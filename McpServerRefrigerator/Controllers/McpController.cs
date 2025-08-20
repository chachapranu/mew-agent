using Microsoft.AspNetCore.Mvc;
using McpServerRefrigerator.Models;
using McpServerRefrigerator.Services;

namespace McpServerRefrigerator.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly ToolExecutionService _toolExecutionService;
    private readonly ILogger<McpController> _logger;

    public McpController(ToolExecutionService toolExecutionService, ILogger<McpController> logger)
    {
        _toolExecutionService = toolExecutionService;
        _logger = logger;
    }

    [HttpGet("tools")]
    public ActionResult<List<ToolDefinition>> GetAvailableTools()
    {
        _logger.LogInformation("Tool discovery requested");
        var tools = _toolExecutionService.GetAvailableTools();
        return Ok(tools);
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ToolResponse>> ExecuteTool([FromBody] ToolRequest request)
    {
        if (string.IsNullOrEmpty(request.ToolName))
        {
            return BadRequest(new ToolResponse 
            { 
                Success = false, 
                Error = "Tool name is required" 
            });
        }

        _logger.LogInformation($"Executing tool: {request.ToolName}");
        var response = await _toolExecutionService.ExecuteToolAsync(request);
        
        if (response.Success)
        {
            return Ok(response);
        }
        
        return StatusCode(500, response);
    }

    [HttpGet("health")]
    public ActionResult<object> HealthCheck()
    {
        return Ok(new 
        { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            service = "MCP Server - Smart Refrigerator"
        });
    }
}