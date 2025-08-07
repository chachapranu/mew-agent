using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace MewAgent.Services;

public class MewAgentService
{
    private readonly ILogger<MewAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HybridMcpService _hybridMcpService;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ChatHistory _chatHistory;

    private const string SystemPrompt = @"You are Mew, a friendly and proactive AI assistant for a smart refrigerator. 
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
        HybridMcpService hybridMcpService,
        ILogger<MewAgentService> logger)
    {
        _configuration = configuration;
        _hybridMcpService = hybridMcpService;
        _logger = logger;

        // Build the kernel
        var builder = Kernel.CreateBuilder();
        
        // Add OpenAI chat completion
        var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not configured");
        var modelId = configuration["OpenAI:ModelId"] ?? "gpt-4";
        
        builder.AddOpenAIChatCompletion(modelId, apiKey);
        
        _kernel = builder.Build();
        
        // Plugin will be loaded async in InitializeAsync method
        
        // Get chat service
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        
        // Initialize chat history with system prompt
        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(SystemPrompt);
        
        _logger.LogInformation("Mew Agent initialized with model: {Model}", modelId);
    }

    public async Task InitializeAsync()
    {
        // Register the MCP/HTTP hybrid plugin
        var plugin = await _hybridMcpService.CreatePluginAsync();
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
            
            // Add user message to history
            _chatHistory.AddUserMessage(userMessage);
            
            // Configure execution settings for function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = _configuration.GetValue<double>("Agent:Temperature", 0.7),
                MaxTokens = _configuration.GetValue<int>("Agent:MaxTokens", 4000)
            };
            
            // Get response from AI with automatic function calling
            var response = await _chatService.GetChatMessageContentAsync(
                _chatHistory,
                executionSettings,
                _kernel);
            
            // Add assistant response to history
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
        return _chatHistory.Count - 1; // Subtract system message
    }

    public async Task<bool> CheckConnectionAsync()
    {
        return await _hybridMcpService.CheckConnectionAsync();
    }
}