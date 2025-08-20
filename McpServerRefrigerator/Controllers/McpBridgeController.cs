using Microsoft.AspNetCore.Mvc;
using McpServerRefrigerator.Models;
using McpServerRefrigerator.Services;
using System.Text.Json;

namespace McpServerRefrigerator.Controllers;

// Bridge controller that translates between MCP protocol and existing HTTP endpoints
[ApiController]
[Route("/")]
public class McpBridgeController : ControllerBase
{
    private readonly ToolExecutionService _toolExecutionService;
    private readonly ILogger<McpBridgeController> _logger;

    public McpBridgeController(ToolExecutionService toolExecutionService, ILogger<McpBridgeController> logger)
    {
        _toolExecutionService = toolExecutionService;
        _logger = logger;
    }

    // MCP protocol endpoint for messages
    [HttpPost("messages")]
    public async Task<IActionResult> HandleMcpMessage([FromBody] JsonElement request)
    {
        try
        {
            var method = request.GetProperty("method").GetString();
            var id = request.TryGetProperty("id", out var idProp) ? idProp.GetRawText() : "null";

            _logger.LogInformation("Received MCP message: {Method}", method);

            switch (method)
            {
                case "initialize":
                    return Ok(CreateInitializeResponse(id));
                    
                case "tools/list":
                    return Ok(await CreateToolsListResponse(id));
                    
                case "tools/call":
                    var parameters = request.GetProperty("params");
                    return Ok(await CreateToolCallResponse(id, parameters));
                    
                default:
                    return Ok(CreateErrorResponse(id, -32601, $"Method not found: {method}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP message");
            return Ok(CreateErrorResponse("null", -32603, $"Internal error: {ex.Message}"));
        }
    }

    // SSE endpoint for MCP client connections
    [HttpPost("sse")]
    public async Task HandleSseConnection()
    {
        _logger.LogInformation("[MCP-CONN] SSE connection established from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
        
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["Connection"] = "keep-alive";
        Response.Headers["Access-Control-Allow-Origin"] = "*";

        try
        {
            // Read the incoming JSON-RPC request from the body
            using var reader = new StreamReader(Request.Body);
            var requestJson = await reader.ReadToEndAsync();
            _logger.LogInformation("[MCP-REQ] Received request: {Request}", requestJson);

            var request = JsonSerializer.Deserialize<JsonElement>(requestJson);
            var method = request.GetProperty("method").GetString();
            var id = request.TryGetProperty("id", out var idProp) ? idProp.GetRawText() : "null";

            _logger.LogInformation("[MCP-METHOD] Processing: {Method} (ID: {Id})", method, id);

            object response = method switch
            {
                "initialize" => CreateInitializeResponse(id),
                "tools/list" => await CreateToolsListResponse(id),
                "tools/call" => await CreateToolCallResponse(id, request.GetProperty("params")),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
            };

            // Send response as SSE event
            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await Response.WriteAsync($"data: {responseJson}\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SSE request");
            var errorResponse = CreateErrorResponse("null", -32603, $"Internal error: {ex.Message}");
            var errorJson = JsonSerializer.Serialize(errorResponse);
            await Response.WriteAsync($"data: {errorJson}\n\n");
            await Response.Body.FlushAsync();
        }
    }

    private object CreateInitializeResponse(string id)
    {
        return new
        {
            jsonrpc = "2.0",
            id = JsonSerializer.Deserialize<object>(id),
            result = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "McpServerRefrigerator",
                    version = "1.0.0"
                }
            }
        };
    }

    private async Task<object> CreateToolsListResponse(string id)
    {
        var tools = _toolExecutionService.GetAvailableTools();
        
        var mcpTools = tools.Select(tool => new
        {
            name = tool.Name,
            description = tool.Description,
            inputSchema = new
            {
                type = "object",
                properties = tool.Parameters.ToDictionary(
                    p => p.Name,
                    p => new
                    {
                        type = p.Type,
                        description = p.Description,
                        required = p.Required
                    }
                ),
                required = tool.Parameters.Where(p => p.Required).Select(p => p.Name).ToArray()
            }
        }).ToArray();

        return new
        {
            jsonrpc = "2.0",
            id = JsonSerializer.Deserialize<object>(id),
            result = new
            {
                tools = mcpTools
            }
        };
    }

    private async Task<object> CreateToolCallResponse(string id, JsonElement parameters)
    {
        try
        {
            var toolName = parameters.GetProperty("name").GetString();
            var toolArgs = parameters.TryGetProperty("arguments", out var args) ? 
                JsonSerializer.Deserialize<Dictionary<string, object>>(args.GetRawText()) : 
                new Dictionary<string, object>();

            _logger.LogInformation("[TOOL-CALL] Executing: {ToolName}", toolName);
            _logger.LogInformation("[TOOL-ARGS] Parameters: {@Arguments}", toolArgs);

            // Call existing tool execution service
            var toolRequest = new ToolRequest
            {
                ToolName = toolName!,
                Parameters = toolArgs ?? new Dictionary<string, object>()
            };

            var startTime = DateTime.UtcNow;
            var toolResponse = await _toolExecutionService.ExecuteToolAsync(toolRequest);
            var duration = DateTime.UtcNow - startTime;

            if (toolResponse.Success)
            {
                _logger.LogInformation("[TOOL-SUCCESS] {ToolName} completed in {Duration:F2}ms", toolName, duration.TotalMilliseconds);
                _logger.LogInformation("[TOOL-RESULT] Output: {@Result}", toolResponse.Result);
                
                return new
                {
                    jsonrpc = "2.0",
                    id = JsonSerializer.Deserialize<object>(id),
                    result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = JsonSerializer.Serialize(toolResponse.Result, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    WriteIndented = true
                                })
                            }
                        }
                    }
                };
            }
            else
            {
                _logger.LogError("[TOOL-ERROR] {ToolName} failed in {Duration:F2}ms: {Error}", toolName, duration.TotalMilliseconds, toolResponse.Error);
                return CreateErrorResponse(id, -32603, toolResponse.Error ?? "Tool execution failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TOOL-EXCEPTION] Tool execution failed: {Message}", ex.Message);
            return CreateErrorResponse(id, -32603, $"Tool execution error: {ex.Message}");
        }
    }

    private object CreateErrorResponse(string id, int code, string message)
    {
        return new
        {
            jsonrpc = "2.0",
            id = JsonSerializer.Deserialize<object>(id),
            error = new
            {
                code = code,
                message = message
            }
        };
    }

}