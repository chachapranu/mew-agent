using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace MewAgent.Services;

// MCP client service using official MCP C# SDK
public class McpClientService : IDisposable
{
    private readonly ILogger<McpClientService> _logger;
    private readonly IConfiguration _configuration;
    private IMcpClient? _mcpClient;
    private IClientTransport? _transport;

    public McpClientService(
        IConfiguration configuration,
        ILogger<McpClientService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<KernelPlugin> CreatePluginAsync()
    {
        _logger.LogInformation("Connecting to MCP server using official SDK");
        
        try
        {
            // Create HTTP transport to connect to HTTP-based MCP server
            var serverUrl = _configuration["McpServer:HttpUrl"] ?? "http://localhost:5100";
            var sseEndpoint = $"{serverUrl}/sse";
            
            _transport = new SseClientTransport(new SseClientTransportOptions
            {
                Name = "MewAgentMcpClient",
                Endpoint = new Uri(sseEndpoint),
                TransportMode = HttpTransportMode.StreamableHttp
            });

            // Create MCP client using official factory
            _mcpClient = await McpClientFactory.CreateAsync(_transport);
            _logger.LogInformation("Successfully connected to MCP server at {Url}", serverUrl);

            // Discover available tools using official SDK
            _logger.LogInformation("Attempting to discover tools from MCP server...");
            var tools = await _mcpClient.ListToolsAsync();
            _logger.LogInformation("ListToolsAsync returned {Count} tools", tools?.Count() ?? 0);
            
            if (tools == null || !tools.Any())
            {
                _logger.LogWarning("No tools discovered from MCP server");
                return KernelPluginFactory.CreateFromFunctions("McpTools", "Empty MCP plugin", new List<KernelFunction>());
            }

            _logger.LogInformation("Discovered {Count} tools from MCP server", tools.Count());

            // Create kernel functions from discovered tools
            var functions = new List<KernelFunction>();
            
            foreach (var tool in tools)
            {
                var function = CreateKernelFunctionFromTool(tool);
                functions.Add(function);
                _logger.LogInformation("Created function: {Name} - {Description}", tool.Name, tool.Description);
            }

            var plugin = KernelPluginFactory.CreateFromFunctions(
                "McpTools",
                "MCP-powered tools via official SDK",
                functions);

            _logger.LogInformation("Created MCP plugin with {Count} functions", functions.Count);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MCP plugin: {ErrorMessage}", ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            
            // Return empty plugin on failure so agent can still work with timers
            return KernelPluginFactory.CreateFromFunctions("McpTools", "Failed MCP plugin", new List<KernelFunction>());
        }
    }


    private KernelFunction CreateKernelFunctionFromTool(dynamic tool)
    {
        string toolName = tool.Name;
        string toolDescription = tool.Description ?? "MCP Tool";
        
        return KernelFunctionFactory.CreateFromMethod(
            method: async (KernelArguments args) => await CallMcpToolAsync(toolName, args),
            functionName: toolName,
            description: toolDescription);
    }

    private async Task<string> CallMcpToolAsync(string toolName, KernelArguments args)
    {
        try
        {
            _logger.LogInformation("Calling MCP tool: {ToolName} with args: {@Args}", toolName, args);

            if (_mcpClient == null)
            {
                _logger.LogError("MCP client is not initialized");
                return $"MCP client not available for tool: {toolName}";
            }

            // Convert KernelArguments to IReadOnlyDictionary for MCP client
            var toolArgs = args.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value) as IReadOnlyDictionary<string, object?>;

            // Call tool using official MCP client
            var result = await _mcpClient.CallToolAsync(toolName, toolArgs);
            
            _logger.LogInformation("MCP tool {ToolName} executed successfully", toolName);
            
            // Return result as JSON string
            return JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call MCP tool: {ToolName}", toolName);
            return $"Error calling tool {toolName}: {ex.Message}";
        }
    }

    public void Dispose()
    {
        // Note: MCP client interfaces don't implement IDisposable in current version
        // Cleanup will be handled by the underlying transport implementation
    }
}

