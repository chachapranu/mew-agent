using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;
using Shared;

namespace MewAgent.Services;

// proper MCP client service that connects to actual MCP server
public class McpClientService
{
    private readonly ILogger<McpClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _mcpServerUrl;

    public McpClientService(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<McpClientService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
        _mcpServerUrl = configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
    }

    public async Task<KernelPlugin> CreatePluginAsync()
    {
        _logger.LogInformation("Discovering MCP tools from server: {Url}", _mcpServerUrl);
        
        try
        {
            // discover available tools from MCP server
            var toolsResponse = await _httpClient.GetAsync($"{_mcpServerUrl}/api/mcp/tools");
            toolsResponse.EnsureSuccessStatusCode();
            
            var toolsJson = await toolsResponse.Content.ReadAsStringAsync();
            var tools = JsonSerializer.Deserialize<List<ToolDefinition>>(toolsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tools == null || !tools.Any())
            {
                throw new InvalidOperationException("No tools discovered from MCP server");
            }

            _logger.LogInformation("Discovered {Count} tools from MCP server", tools.Count);

            // create kernel functions from discovered tools
            var functions = new List<KernelFunction>();
            
            foreach (var tool in tools)
            {
                var function = CreateKernelFunctionFromTool(tool);
                functions.Add(function);
                _logger.LogDebug("Created function: {Name} - {Description}", tool.Name, tool.Description);
            }

            var plugin = KernelPluginFactory.CreateFromFunctions(
                "McpRefrigerator",
                "MCP-powered refrigerator control and monitoring",
                functions);

            _logger.LogInformation("Created MCP plugin with {Count} functions", functions.Count);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MCP plugin from server: {Url}", _mcpServerUrl);
            throw;
        }
    }

    private KernelFunction CreateKernelFunctionFromTool(ToolDefinition tool)
    {
        return KernelFunctionFactory.CreateFromMethod(
            method: async (KernelArguments args) => await CallMcpToolAsync(tool.Name, args),
            functionName: tool.Name,
            description: tool.Description);
    }

    private async Task<string> CallMcpToolAsync(string toolName, KernelArguments args)
    {
        try
        {
            _logger.LogDebug("Calling MCP tool: {ToolName}", toolName);

            // prepare tool request
            var request = new ToolRequest
            {
                ToolName = toolName,
                Parameters = args.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? new object())
            };

            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            // call MCP server
            var response = await _httpClient.PostAsync($"{_mcpServerUrl}/api/mcp/execute", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var toolResponse = JsonSerializer.Deserialize<ToolResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (toolResponse == null)
            {
                throw new InvalidOperationException($"Invalid response from MCP server for tool: {toolName}");
            }

            if (!toolResponse.Success)
            {
                throw new InvalidOperationException($"MCP tool execution failed: {toolResponse.Error}");
            }

            _logger.LogDebug("MCP tool {ToolName} executed successfully in {ExecutionTime}ms", toolName, toolResponse.ExecutionTimeMs);
            
            // serialize result to string for semantic kernel
            if (toolResponse.Result == null)
                return string.Empty;
            
            if (toolResponse.Result is string strResult)
                return strResult;
                
            return JsonSerializer.Serialize(toolResponse.Result, new JsonSerializerOptions
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
}

// using shared models from Shared project