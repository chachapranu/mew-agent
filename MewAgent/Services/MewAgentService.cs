using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;

namespace MewAgent.Services;

public class MewAgentService
{
    private readonly ILogger<MewAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ProperMcpClientService _mcpClientService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ChatHistory _chatHistory;

    // system prompt for the AI - defines personality and capabilities  
    private const string SystemPrompt = @"You are Mew, a friendly AI assistant for a smart refrigerator. 
You help users manage their kitchen, food inventory, recipes, and cooking activities.

Your personality:
- Enthusiastic about cooking and food
- Proactive in suggesting recipes and meal planning  
- Helpful with kitchen organization
- Knowledgeable about food safety and storage

You have access to refrigerator tools to:
- Check and adjust temperatures
- Monitor system health
- View food inventory
- Suggest recipes based on available ingredients

Always explain what you're doing when using tools, and provide helpful context about why certain actions are recommended.
When users want to cook, be encouraging and suggest complementary activities like playing music or setting timers.";

    public MewAgentService(
        IConfiguration configuration,
        ProperMcpClientService mcpClientService,
        ILogger<MewAgentService> logger)
    {
        _configuration = configuration;
        _mcpClientService = mcpClientService;
        _logger = logger;

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
        _logger.LogInformation("Loading MCP plugins...");
        
        var plugin = await _mcpClientService.CreatePluginAsync();
        if (plugin != null)
        {
            _kernel.Plugins.Add(plugin);
            _logger.LogInformation("Added {PluginName} plugin with {FunctionCount} functions", 
                plugin.Name, plugin.Count());
        }
        else
        {
            _logger.LogWarning("Failed to create refrigerator plugin");
        }
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
            
            _chatHistory.AddAssistantMessage(response.Content ?? string.Empty);
            
            _logger.LogInformation("Generated response with {Length} characters", response.Content?.Length ?? 0);
            
            return response.Content ?? "I'm sorry, I couldn't generate a response.";
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
}