using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
// Add this at the top of your file if you know the namespace

namespace MewAgent.Services;

public class HybridMcpService : IAsyncDisposable
{
    private readonly ILogger<HybridMcpService> _logger;
    private readonly IConfiguration _configuration;
    private readonly McpClientService _fallbackHttpClient;
    private IMcpClient? _mcpClient;
    private bool _isConnected;

    public HybridMcpService(
        IConfiguration configuration, 
        McpClientService fallbackHttpClient,
        ILogger<HybridMcpService> logger)
    {
        _configuration = configuration;
        _fallbackHttpClient = fallbackHttpClient;
        _logger = logger;
    }

    public async Task<KernelPlugin?> CreatePluginAsync()
    {
        // First try to connect using official MCP protocol
        var mcpPlugin = await TryCreateMcpPluginAsync();
        if (mcpPlugin != null)
        {
            _logger.LogInformation("Successfully created MCP plugin using native SK MCP support");
            return mcpPlugin;
        }

        // Fallback to HTTP client approach
        _logger.LogInformation("MCP connection failed, falling back to HTTP client approach");
        return await CreateHttpPluginAsync();
    }

    private async Task<KernelPlugin?> TryCreateMcpPluginAsync()
    {
        try
        {
            var baseUrl = _configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
            
            // Try SSE transport first (standard MCP)
            try
            {
                var sseTransport = new SseClientTransport(new SseClientTransportOptions 
                {
                    Endpoint = new Uri($"{baseUrl}/sse")
                });

                var options = new McpClientOptions 
                {
                    ClientInfo = new() 
                    {
                        Name = "Mew Agent",
                        Version = "1.0.0"
                    }
                };

                _mcpClient = await McpClientFactory.CreateAsync(sseTransport, options);
                _isConnected = true;

                var tools = await _mcpClient.ListToolsAsync();
                _logger.LogInformation("Connected via MCP SSE, discovered {Count} tools", tools.Count);
                
                return CreateKernelPluginFromMcp(tools);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SSE transport failed, this is expected if server doesn't support MCP protocol");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MCP connection failed");
            return null;
        }
    }

    private async Task<KernelPlugin?> CreateHttpPluginAsync()
    {
        try
        {
            // Check if HTTP server is available
            if (!await _fallbackHttpClient.CheckHealthAsync())
            {
                _logger.LogWarning("HTTP server is not available");
                return null;
            }

            // Create plugin using existing RefrigeratorPlugin
            var pluginLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<MewAgent.Plugins.RefrigeratorPlugin>();
            var plugin = new MewAgent.Plugins.RefrigeratorPlugin(_fallbackHttpClient, pluginLogger);
            
            var functions = new List<KernelFunction>
            {
                KernelFunctionFactory.CreateFromMethod(
                    plugin.GetTemperatureAsync,
                    "GetTemperature",
                    "Get current refrigerator and freezer temperature settings"),
                    
                KernelFunctionFactory.CreateFromMethod(
                    plugin.SetTemperatureAsync,
                    "SetTemperature", 
                    "Set refrigerator and/or freezer temperature"),
                    
                KernelFunctionFactory.CreateFromMethod(
                    plugin.GetDiagnosticsAsync,
                    "GetDiagnostics",
                    "Get system health and maintenance information"),
                    
                KernelFunctionFactory.CreateFromMethod(
                    plugin.GetInventoryAsync,
                    "GetInventory",
                    "Get current food inventory in refrigerator"),
                    
                KernelFunctionFactory.CreateFromMethod(
                    plugin.GetRecipeSuggestionsAsync,
                    "GetRecipeSuggestions",
                    "Get recipe suggestions based on available ingredients")
            };

            _logger.LogInformation("Created HTTP plugin with {Count} functions", functions.Count);
            return KernelPluginFactory.CreateFromFunctions("Refrigerator", "Smart refrigerator tools", functions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create HTTP plugin");
            return null;
        }
    }

    private KernelPlugin CreateKernelPluginFromMcp(IList<McpClientTool> mcpTools)
    {
        var functions = new List<KernelFunction>();

        foreach (var tool in mcpTools)
        {
            var function = KernelFunctionFactory.CreateFromMethod(
                method: async (KernelArguments args) => await InvokeMcpToolAsync(tool.Name, args),
                functionName: tool.Name,
                description: tool.Description ?? $"MCP tool: {tool.Name}");

            functions.Add(function);
        }

        return KernelPluginFactory.CreateFromFunctions("RefrigeratorMcp", "MCP refrigerator tools", functions);
    }

    private async Task<string> InvokeMcpToolAsync(string toolName, KernelArguments arguments)
    {
        if (_mcpClient == null || !_isConnected)
        {
            return "MCP client not connected";
        }

        try
        {
            var mcpArguments = new Dictionary<string, object?>();
            foreach (var arg in arguments)
            {
                mcpArguments[arg.Key] = arg.Value;
            }

            var result = await _mcpClient.CallToolAsync(toolName, mcpArguments);
            
            // Extract text content from result
            var textContent = result.Content.FirstOrDefault();
            if (textContent != null)
            {
                // Try to access text property - this may vary based on MCP implementation
                var contentText = textContent.ToString();
                return !string.IsNullOrEmpty(contentText) ? contentText : "Tool executed successfully";
            }
            return "Tool executed successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking MCP tool {ToolName}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    public async Task<bool> CheckConnectionAsync()
    {
        if (_mcpClient != null && _isConnected)
        {
            return true;
        }
        
        return await _fallbackHttpClient.CheckHealthAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient != null)
        {
            try
            {
                await _mcpClient.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MCP client");
            }
            finally
            {
                _mcpClient = null;
                _isConnected = false;
            }
        }
    }
}