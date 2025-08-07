using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;

namespace MewAgent.Services;

public class McpClientService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClientService> _logger;
    private readonly string _baseUrl;
    private List<ToolDefinition>? _cachedTools;

    public McpClientService(HttpClient httpClient, IConfiguration configuration, ILogger<McpClientService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["McpServer:BaseUrl"] ?? "http://localhost:5100";
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int>("McpServer:Timeout", 30));
    }

    public async Task<List<ToolDefinition>> DiscoverToolsAsync()
    {
        if (_cachedTools != null)
            return _cachedTools;

        try
        {
            _logger.LogInformation("Discovering MCP tools from {BaseUrl}", _baseUrl);
            var response = await _httpClient.GetAsync("/api/mcp/tools");
            response.EnsureSuccessStatusCode();
            
            _cachedTools = await response.Content.ReadFromJsonAsync<List<ToolDefinition>>() 
                ?? new List<ToolDefinition>();
            
            _logger.LogInformation("Discovered {Count} MCP tools", _cachedTools.Count);
            foreach (var tool in _cachedTools)
            {
                _logger.LogDebug("Tool: {Name} - {Description}", tool.Name, tool.Description);
            }
            
            return _cachedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover MCP tools");
            return new List<ToolDefinition>();
        }
    }

    public async Task<ToolResponse> ExecuteToolAsync(string toolName, Dictionary<string, object>? parameters = null)
    {
        try
        {
            _logger.LogInformation("Executing MCP tool: {ToolName}", toolName);
            
            var request = new ToolRequest
            {
                ToolName = toolName,
                Parameters = parameters ?? new Dictionary<string, object>()
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/execute", request);
            
            var toolResponse = await response.Content.ReadFromJsonAsync<ToolResponse>();
            if (toolResponse == null)
            {
                return new ToolResponse
                {
                    Success = false,
                    Error = "Invalid response from MCP server"
                };
            }

            if (toolResponse.Success)
            {
                _logger.LogInformation("Tool {ToolName} executed successfully in {Time}ms", 
                    toolName, toolResponse.ExecutionTimeMs);
            }
            else
            {
                _logger.LogWarning("Tool {ToolName} failed: {Error}", 
                    toolName, toolResponse.Error);
            }

            return toolResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error executing tool {ToolName}", toolName);
            return new ToolResponse
            {
                Success = false,
                Error = $"Network error: {ex.Message}"
            };
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Timeout executing tool {ToolName}", toolName);
            return new ToolResponse
            {
                Success = false,
                Error = "Request timeout"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing tool {ToolName}", toolName);
            return new ToolResponse
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/mcp/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}