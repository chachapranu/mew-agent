using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MewAgent.Services;
using Shared;

namespace MewAgent.Plugins;

public class RefrigeratorPlugin
{
    private readonly McpClientService _mcpClient;
    private readonly ILogger<RefrigeratorPlugin> _logger;

    public RefrigeratorPlugin(McpClientService mcpClient, ILogger<RefrigeratorPlugin> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    [KernelFunction("GetTemperature")]
    [Description("Get the current temperature settings of the refrigerator and freezer")]
    public async Task<string> GetTemperatureAsync()
    {
        _logger.LogDebug("Getting temperature from refrigerator");
        var response = await _mcpClient.ExecuteToolAsync("GetTemperature");
        
        if (!response.Success)
            return $"Failed to get temperature: {response.Error}";

        if (response.Result is JsonElement json)
        {
            var temp = json.Deserialize<TemperatureSettings>();
            if (temp != null)
            {
                return $"Current temperatures - Fridge: {temp.FridgeTemp}°F, Freezer: {temp.FreezerTemp}°F, Mode: {temp.Mode}";
            }
        }

        return "Temperature data unavailable";
    }

    [KernelFunction("SetTemperature")]
    [Description("Set the refrigerator and/or freezer temperature")]
    public async Task<string> SetTemperatureAsync(
        [Description("Fridge temperature in Fahrenheit (32-45°F)")] double? fridgeTemp = null,
        [Description("Freezer temperature in Fahrenheit (-10 to 10°F)")] double? freezerTemp = null)
    {
        _logger.LogDebug("Setting temperature - Fridge: {FridgeTemp}, Freezer: {FreezerTemp}", 
            fridgeTemp, freezerTemp);

        var parameters = new Dictionary<string, object>();
        if (fridgeTemp.HasValue)
            parameters["fridgeTemp"] = fridgeTemp.Value;
        if (freezerTemp.HasValue)
            parameters["freezerTemp"] = freezerTemp.Value;

        if (parameters.Count == 0)
            return "No temperature values provided";

        var response = await _mcpClient.ExecuteToolAsync("SetTemperature", parameters);
        
        if (!response.Success)
            return $"Failed to set temperature: {response.Error}";

        if (response.Result is JsonElement json)
        {
            var temp = json.Deserialize<TemperatureSettings>();
            if (temp != null)
            {
                return $"Temperature updated - Fridge: {temp.FridgeTemp}°F, Freezer: {temp.FreezerTemp}°F";
            }
        }

        return "Temperature updated successfully";
    }

    [KernelFunction("GetDiagnostics")]
    [Description("Get system diagnostics and health information for the refrigerator")]
    public async Task<string> GetDiagnosticsAsync()
    {
        _logger.LogDebug("Getting system diagnostics");
        var response = await _mcpClient.ExecuteToolAsync("GetDiagnostics");
        
        if (!response.Success)
            return $"Failed to get diagnostics: {response.Error}";

        if (response.Result is JsonElement json)
        {
            var diag = json.Deserialize<SystemDiagnostics>();
            if (diag != null)
            {
                var alerts = diag.ActiveAlerts.Any() 
                    ? $"\nActive Alerts: {string.Join(", ", diag.ActiveAlerts)}" 
                    : "";
                
                return $"System Status: {diag.Status}\n" +
                       $"Filter Days Remaining: {diag.FilterDaysRemaining}\n" +
                       $"Power Usage: {diag.PowerUsageKwh} kWh\n" +
                       $"Last Maintenance: {diag.LastMaintenance:yyyy-MM-dd}" +
                       alerts;
            }
        }

        return "Diagnostics data unavailable";
    }

    [KernelFunction("GetInventory")]
    [Description("Get the current food inventory in the refrigerator")]
    public async Task<string> GetInventoryAsync()
    {
        _logger.LogDebug("Getting inventory");
        var response = await _mcpClient.ExecuteToolAsync("GetInventory");
        
        if (!response.Success)
            return $"Failed to get inventory: {response.Error}";

        if (response.Result is JsonElement json)
        {
            var items = json.Deserialize<List<InventoryItem>>();
            if (items != null && items.Any())
            {
                var inventory = "Current inventory:\n";
                foreach (var item in items)
                {
                    var expiry = item.ExpirationDate.HasValue 
                        ? $" (expires: {item.ExpirationDate.Value:yyyy-MM-dd})" 
                        : "";
                    inventory += $"- {item.Name}: {item.Quantity} {item.Unit}{expiry}\n";
                }
                return inventory;
            }
        }

        return "No items in inventory";
    }

    [KernelFunction("GetRecipeSuggestions")]
    [Description("Get recipe suggestions based on available ingredients in the refrigerator")]
    public async Task<string> GetRecipeSuggestionsAsync()
    {
        _logger.LogDebug("Getting recipe suggestions");
        var response = await _mcpClient.ExecuteToolAsync("GetRecipeSuggestions");
        
        if (!response.Success)
            return $"Failed to get recipes: {response.Error}";

        if (response.Result is JsonElement json)
        {
            var recipes = json.Deserialize<List<Recipe>>();
            if (recipes != null && recipes.Any())
            {
                var suggestions = "Recipe suggestions based on your ingredients:\n\n";
                foreach (var recipe in recipes)
                {
                    suggestions += $"**{recipe.Name}**\n";
                    suggestions += $"- Prep: {recipe.PrepTimeMinutes} min, Cook: {recipe.CookTimeMinutes} min\n";
                    suggestions += $"- Servings: {recipe.Servings}\n";
                    suggestions += $"- Ingredients: {string.Join(", ", recipe.Ingredients)}\n";
                    suggestions += $"- Instructions: {recipe.Instructions}\n\n";
                }
                return suggestions;
            }
        }

        return "No recipe suggestions available";
    }
}