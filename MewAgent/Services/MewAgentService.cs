using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;
using MewAgent.Plugins;
using MewAgent.Models;

namespace MewAgent.Services;

public class MewAgentService
{
    private readonly ILogger<MewAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly McpClientService _mcpClientService;
    private readonly ITimerService _timerService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ChatHistory _chatHistory;
    private readonly TimerPlugin _timerPlugin;
    
    // for displaying timer notifications to user
    public Action<string>? MessageDisplayer 
    { 
        get => _messageDisplayer;
        set 
        {
            _messageDisplayer = value;
            // set up timer service integration when MessageDisplayer is assigned
            SetupTimerServiceIntegration();
        }
    }
    private Action<string>? _messageDisplayer;

    // system prompt for the AI - simple and direct
    private const string SystemPrompt = @"You are Mew, an AI assistant for a smart refrigerator system.

You have access to refrigerator tools via MCP (Model Context Protocol) and timer tools.

When users ask about refrigerator data:
1. Use the appropriate MCP tool to get the information
2. Present the results in natural, conversational language
3. Don't show raw JSON data - interpret and explain it clearly

For example:
- Temperature queries: 'The refrigerator is set to 37°F (freezer at 0°F, Normal mode)'
- Inventory queries: 'You have 2 eggs, 1 gallon of milk, and 3 apples'

When users request delayed actions, use timer tools appropriately.
Be direct, helpful, and conversational.";


    public MewAgentService(
        IConfiguration configuration,
        McpClientService mcpClientService,
        ITimerService timerService,
        ILogger<MewAgentService> logger)
    {
        _configuration = configuration;
        _mcpClientService = mcpClientService;
        _timerService = timerService;
        _logger = logger;
        
        // create timer plugin (using a simple logger for now)
        var timerLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<TimerPlugin>();
        _timerPlugin = new TimerPlugin(_timerService, timerLogger);

        var builder = Kernel.CreateBuilder();
        
        var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
        var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4";
        var endpoint = configuration["OpenAI:Endpoint"];
        
        if (!string.IsNullOrEmpty(endpoint))
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
            
            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                httpClient: httpClient);
            
            _logger.LogInformation($"Using custom LLM endpoint: {endpoint} with model: {modelId}");
        }
        else
        {
            builder.AddOpenAIChatCompletion(modelId, apiKey);
            _logger.LogInformation($"Using standard OpenAI with model: {modelId}");
        }
        
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(SystemPrompt);
        
        _logger.LogInformation("Mew Agent initialized successfully");
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Loading plugins...");
        
        // load MCP plugin
        var mcpPlugin = await _mcpClientService.CreatePluginAsync();
        if (mcpPlugin != null)
        {
            _kernel.Plugins.Add(mcpPlugin);
            _logger.LogInformation("Added {PluginName} plugin with {FunctionCount} functions", 
                mcpPlugin.Name, mcpPlugin.Count());
                
            // Log each available function
            foreach (var function in mcpPlugin)
            {
                _logger.LogInformation("  - Function: {FunctionName} - {Description}", 
                    function.Name, function.Description);
            }
        }
        else
        {
            _logger.LogWarning("Failed to create refrigerator plugin");
        }
        
        // add timer plugin
        _kernel.Plugins.AddFromObject(_timerPlugin, "TimerPlugin");
        _logger.LogInformation("Added Timer plugin for proactive behavior");
        
        // note: timer service integration will be set up when MessageDisplayer is assigned
        
        _logger.LogInformation("All plugins loaded successfully");
    }

    public async Task<string> ProcessMessageAsync(string userMessage)
    {
        try
        {
            _logger.LogInformation("Processing user message: {Message}", userMessage);
            
            _chatHistory.AddUserMessage(userMessage);
            
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = _configuration.GetValue<double>("Agent:Temperature", 0.7),
                MaxTokens = _configuration.GetValue<int>("Agent:MaxTokens", 4000)
            };
            
            var response = await _chatService.GetChatMessageContentAsync(
                _chatHistory,
                executionSettings,
                _kernel);
            
            var cleanContent = CleanLLMResponse(response.Content ?? string.Empty);
            _chatHistory.AddAssistantMessage(cleanContent);
            
            _logger.LogInformation("Generated response with {Length} characters", cleanContent.Length);
            
            return cleanContent.Length > 0 ? cleanContent : "I'm sorry, I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            return $"I encountered an error: {ex.Message}. Please try again.";
        }
    }

    public void ClearHistory()
    {
        _chatHistory.Clear();
        _chatHistory.AddSystemMessage(SystemPrompt);
        _logger.LogInformation("Chat history cleared");
    }

    public int GetHistoryCount()
    {
        return _chatHistory.Count - 1;
    }
    
    // set up timer service integration
    private void SetupTimerServiceIntegration()
    {
        // configure timer service to call LLM
        if (_timerService is TimerService timerService)
        {
            timerService.LLMInvoker = async (prompt) =>
            {
                try
                {
                    // create a temporary chat history for timer-triggered LLM calls
                    var tempHistory = new ChatHistory();
                    tempHistory.AddSystemMessage(SystemPrompt); // reuse the same system prompt
                    tempHistory.AddUserMessage(prompt);
                    
                    var executionSettings = new OpenAIPromptExecutionSettings
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        Temperature = 0.7,
                        MaxTokens = 2000
                    };
                    
                    var response = await _chatService.GetChatMessageContentAsync(
                        tempHistory,
                        executionSettings,
                        _kernel);
                    
                    return CleanLLMResponse(response.Content ?? "Timer response generated successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in timer LLM invocation");
                    return $"Error generating timer response: {ex.Message}";
                }
            };
            
            // configure timer service to execute MCP tools
            timerService.ToolInvoker = async (toolName, parameters) =>
            {
                try
                {
                    // find the function in the kernel plugins
                    KernelFunction? function = null;
                    
                    // if toolName contains a dash, it might be in format "PluginName-FunctionName"
                    if (toolName.Contains('-'))
                    {
                        var parts = toolName.Split('-', 2);
                        if (parts.Length == 2)
                        {
                            var pluginName = parts[0];
                            var functionName = parts[1];
                            
                            if (_kernel.Plugins.TryGetFunction(pluginName, functionName, out function))
                            {
                                // found the function
                            }
                        }
                    }
                    
                    // if not found, search through all plugins
                    if (function == null)
                    {
                        foreach (var plugin in _kernel.Plugins)
                        {
                            if (plugin.TryGetFunction(toolName, out function))
                                break;
                        }
                    }
                    
                    if (function == null)
                    {
                        return $"Error: Tool '{toolName}' not found";
                    }
                    
                    // convert parameters to KernelArguments
                    var kernelArgs = new KernelArguments();
                    foreach (var param in parameters)
                    {
                        kernelArgs[param.Key] = param.Value;
                    }
                    
                    // execute the function
                    var result = await function.InvokeAsync(_kernel, kernelArgs);
                    
                    var resultValue = result.GetValue<string>() ?? result.ToString();
                    _logger.LogInformation("Timer tool '{ToolName}' executed successfully", toolName);
                    
                    return resultValue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in timer tool invocation: {ToolName}", toolName);
                    return $"Error executing timer tool '{toolName}': {ex.Message}";
                }
            };
            
            // configure message displayer
            timerService.MessageDisplayer = MessageDisplayer;
            _logger.LogInformation("Timer service MessageDisplayer configured: {IsConfigured}", MessageDisplayer != null);
            
            // subscribe to timer events
            timerService.TimerExpired += OnTimerExpired;
            timerService.ActionCompleted += OnTimerActionCompleted;
            
            _logger.LogInformation("Timer service integration configured");
        }
    }
    
    // handle timer expiration events
    private void OnTimerExpired(object? sender, TimerExpiredEventArgs e)
    {
        _logger.LogInformation("Timer '{TimerName}' expired at {ExpiredAt}", e.Timer.Name, e.ExpiredAt);
    }
    
    // handle timer action completion events
    private void OnTimerActionCompleted(object? sender, TimerActionResult e)
    {
        if (e.Success)
        {
            _logger.LogInformation("Timer action completed successfully: {Message}", e.Message);
        }
        else
        {
            _logger.LogWarning("Timer action failed: {Message}", e.Message);
        }
    }
    
    // get active timers for debugging
    public async Task<List<InternalTimer>> GetActiveTimersAsync()
    {
        return await _timerService.GetActiveTimersAsync();
    }
    
    // cancel timer by ID
    public async Task<bool> CancelTimerAsync(string timerId)
    {
        return await _timerService.CancelTimerAsync(timerId);
    }

    // clean LLM response from reasoning tokens (for models that include them)
    private string CleanLLMResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return response;

        // check if response contains reasoning tokens
        if (!response.Contains("<|channel|>") && !response.Contains("<|message|>"))
            return response; // no cleaning needed

        try
        {
            // extract final message from reasoning tokens
            // pattern: <|channel|>final<|message|>actual_message
            var finalPattern = @"<\|channel\|>final<\|message\|>(.*?)(?:<\|end\||$)";
            var match = System.Text.RegularExpressions.Regex.Match(response, finalPattern, System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // fallback: try to extract any message after the last <|message|> tag
            var lastMessagePattern = @"<\|message\|>([^<]*?)(?:<|$)";
            var matches = System.Text.RegularExpressions.Regex.Matches(response, lastMessagePattern, System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (matches.Count > 0)
            {
                // get the last message
                var lastMatch = matches[matches.Count - 1];
                return lastMatch.Groups[1].Value.Trim();
            }

            // if no patterns match, return original response
            return response;
        }
        catch
        {
            // if regex parsing fails, return original response
            return response;
        }
    }
}