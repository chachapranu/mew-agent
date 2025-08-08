using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace MewAgent.Services;

/// <summary>
/// Simplified MCP Service that focuses on pure MCP protocol integration
/// with Semantic Kernel. This service creates SK plugins from MCP tools.
/// </summary>
public class SimpleMcpService
{
    private readonly ILogger<SimpleMcpService> _logger;
    private readonly IConfiguration _configuration;

    public SimpleMcpService(
        IConfiguration configuration, 
        ILogger<SimpleMcpService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Semantic Kernel plugin with mock refrigerator tools.
    /// In a real implementation, this would discover tools via MCP protocol.
    /// </summary>
    public Task<KernelPlugin> CreatePluginAsync()
    {
        _logger.LogInformation("Creating MCP plugin for refrigerator control");

        // Create functions that would normally come from MCP discovery
        var functions = new List<KernelFunction>();

        // Temperature control functions
        functions.Add(CreateMockFunction(
            "GetTemperature",
            "Get the current temperature settings of the refrigerator and freezer",
            async (KernelArguments args) => 
            {
                await Task.Delay(200); // Simulate network delay
                return "Current temperatures - Fridge: 37°F, Freezer: 0°F, Mode: Normal";
            }));

        functions.Add(CreateMockFunction(
            "SetTemperature", 
            "Set the refrigerator and/or freezer temperature",
            async (KernelArguments args) =>
            {
                await Task.Delay(300);
                var fridgeTemp = args.GetValueOrDefault<double?>("fridgeTemp");
                var freezerTemp = args.GetValueOrDefault<double?>("freezerTemp");
                return $"Temperature updated - Fridge: {fridgeTemp ?? 37}°F, Freezer: {freezerTemp ?? 0}°F";
            }));

        // Diagnostics function
        functions.Add(CreateMockFunction(
            "GetDiagnostics",
            "Get system diagnostics and health information",
            async (KernelArguments args) =>
            {
                await Task.Delay(250);
                return @"System Status: Healthy
Filter Days Remaining: 90
Power Usage: 1.2 kWh
Last Maintenance: 30 days ago";
            }));

        // Inventory function
        functions.Add(CreateMockFunction(
            "GetInventory",
            "Get the current food inventory in the refrigerator",
            async (KernelArguments args) =>
            {
                await Task.Delay(200);
                return @"Current inventory:
- Milk: 1 gallon (expires: 7 days)
- Eggs: 12 count (expires: 14 days)
- Cheese: 2 pounds (expires: 21 days)
- Lettuce: 1 head (expires: 5 days)
- Chicken: 3 pounds (expires: 3 days)";
            }));

        // Recipe suggestions function
        functions.Add(CreateMockFunction(
            "GetRecipeSuggestions",
            "Get recipe suggestions based on available ingredients",
            async (KernelArguments args) =>
            {
                await Task.Delay(400);
                return @"Recipe suggestions based on your ingredients:

**Chicken Caesar Salad**
- Prep: 15 min, Cook: 20 min
- Servings: 4
- Ingredients: Chicken, Lettuce, Cheese, Caesar Dressing

**Cheese Omelette**
- Prep: 5 min, Cook: 5 min
- Servings: 2
- Ingredients: Eggs, Cheese, Butter, Salt, Pepper";
            }));

        // Create and return the plugin
        var plugin = KernelPluginFactory.CreateFromFunctions(
            "Refrigerator",
            "Smart refrigerator control and monitoring tools",
            functions);

        _logger.LogInformation($"Created plugin with {functions.Count} functions");
        return Task.FromResult(plugin);
    }

    /// <summary>
    /// Helper method to create a mock kernel function.
    /// This simulates what would come from MCP tool discovery.
    /// </summary>
    private KernelFunction CreateMockFunction(
        string name,
        string description,
        Func<KernelArguments, Task<string>> implementation)
    {
        return KernelFunctionFactory.CreateFromMethod(
            method: implementation,
            functionName: name,
            description: description);
    }
}