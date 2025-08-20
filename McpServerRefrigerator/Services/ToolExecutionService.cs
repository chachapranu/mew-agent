using System.Diagnostics;
using System.Text.Json;
using McpServerRefrigerator.Models;

namespace McpServerRefrigerator.Services;

public class ToolExecutionService
{
    private readonly RefrigeratorService _refrigeratorService;
    private readonly ILogger<ToolExecutionService> _logger;

    public ToolExecutionService(RefrigeratorService refrigeratorService, ILogger<ToolExecutionService> logger)
    {
        _refrigeratorService = refrigeratorService;
        _logger = logger;
    }

    public List<ToolDefinition> GetAvailableTools()
    {
        return new List<ToolDefinition>
        {
            new()
            {
                Name = "GetTemperature",
                Description = "Get current refrigerator and freezer temperature settings",
                Category = "Temperature Control",
                Parameters = new()
            },
            new()
            {
                Name = "SetTemperature",
                Description = "Set refrigerator and/or freezer temperature",
                Category = "Temperature Control",
                Parameters = new()
                {
                    new() { Name = "fridgeTemp", Type = "number", Required = false, Description = "Fridge temperature in Fahrenheit (32-45)" },
                    new() { Name = "freezerTemp", Type = "number", Required = false, Description = "Freezer temperature in Fahrenheit (-10 to 10)" }
                }
            },
            new()
            {
                Name = "GetDiagnostics",
                Description = "Get system health and maintenance information",
                Category = "Diagnostics",
                Parameters = new()
            },
            new()
            {
                Name = "GetInventory",
                Description = "Get current food inventory in the refrigerator",
                Category = "Inventory",
                Parameters = new()
            },
            new()
            {
                Name = "GetRecipeSuggestions",
                Description = "Get recipe suggestions based on available ingredients",
                Category = "Recipe Assistant",
                Parameters = new()
            }
        };
    }

    public async Task<ToolResponse> ExecuteToolAsync(ToolRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("[TOOL-EXEC] Starting {ToolName} with params: {@Parameters}", request.ToolName, request.Parameters);
        
        try
        {
            object? result = request.ToolName switch
            {
                "GetTemperature" => await _refrigeratorService.GetTemperatureAsync(),
                "SetTemperature" => await ExecuteSetTemperature(request.Parameters),
                "GetDiagnostics" => await _refrigeratorService.GetDiagnosticsAsync(),
                "GetInventory" => await _refrigeratorService.GetInventoryAsync(),
                "GetRecipeSuggestions" => await _refrigeratorService.GetRecipeSuggestionsAsync(),
                _ => throw new NotSupportedException($"Tool '{request.ToolName}' is not supported")
            };

            stopwatch.Stop();
            _logger.LogInformation("[TOOL-DONE] {ToolName} finished in {Duration}ms", request.ToolName, stopwatch.ElapsedMilliseconds);

            return new ToolResponse
            {
                Success = true,
                Result = result,
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[TOOL-FAIL] {ToolName} failed after {Duration}ms", request.ToolName, stopwatch.ElapsedMilliseconds);

            return new ToolResponse
            {
                Success = false,
                Error = ex.Message,
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<TemperatureSettings> ExecuteSetTemperature(Dictionary<string, object> parameters)
    {
        double? fridgeTemp = null;
        double? freezerTemp = null;

        if (parameters.TryGetValue("fridgeTemp", out var fridgeObj))
        {
            fridgeTemp = ConvertToDouble(fridgeObj);
        }

        if (parameters.TryGetValue("freezerTemp", out var freezerObj))
        {
            freezerTemp = ConvertToDouble(freezerObj);
        }

        return await _refrigeratorService.SetTemperatureAsync(fridgeTemp, freezerTemp);
    }

    private double? ConvertToDouble(object value)
    {
        if (value == null) return null;
        
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
                return jsonElement.GetDouble();
            if (jsonElement.ValueKind == JsonValueKind.String && double.TryParse(jsonElement.GetString(), out var parsed))
                return parsed;
        }
        
        if (value is double d) return d;
        if (value is int i) return i;
        if (value is string s && double.TryParse(s, out var result)) return result;
        
        return null;
    }
}