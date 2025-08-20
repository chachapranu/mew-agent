using McpServerRefrigerator.Models;

namespace McpServerRefrigerator.Services;

public class RefrigeratorService
{
    private readonly ILogger<RefrigeratorService> _logger;
    
    private TemperatureSettings _temperatureSettings = new();
    private SystemDiagnostics _diagnostics = new();
    // mock inventory - simple data for testing MCP
    private readonly List<InventoryItem> _inventory = new List<InventoryItem>()
    {
        new InventoryItem() { Name = "Milk", Quantity = 1, Unit = "gallon", ExpirationDate = DateTime.Now.AddDays(7) },
        new InventoryItem() { Name = "Eggs", Quantity = 12, Unit = "count", ExpirationDate = DateTime.Now.AddDays(14) },
        new InventoryItem() { Name = "Cheese", Quantity = 2, Unit = "pounds", ExpirationDate = DateTime.Now.AddDays(21) },
        new InventoryItem() { Name = "Lettuce", Quantity = 1, Unit = "head", ExpirationDate = DateTime.Now.AddDays(5) },
        new InventoryItem() { Name = "Chicken", Quantity = 3, Unit = "pounds", ExpirationDate = DateTime.Now.AddDays(3) }
    };

    public RefrigeratorService(ILogger<RefrigeratorService> logger)
    {
        _logger = logger;
    }

    public async Task<TemperatureSettings> GetTemperatureAsync()
    {
        _logger.LogInformation("Getting temperature settings");
        return await Task.FromResult(_temperatureSettings);
    }

    public async Task<TemperatureSettings> SetTemperatureAsync(double? fridgeTemp, double? freezerTemp)
    {
        
        if (fridgeTemp.HasValue)
        {
            if (fridgeTemp < 32 || fridgeTemp > 45)
                throw new ArgumentException("Fridge temperature must be between 32-45F");
            _temperatureSettings.FridgeTemp = fridgeTemp.Value;
        }
        
        if (freezerTemp.HasValue)
        {
            if (freezerTemp < -10 || freezerTemp > 10)
                throw new ArgumentException("Freezer temperature must be between -10 to 10F");
            _temperatureSettings.FreezerTemp = freezerTemp.Value;
        }
        
        _logger.LogInformation($"Updated temperatures - Fridge: {_temperatureSettings.FridgeTemp}F, Freezer: {_temperatureSettings.FreezerTemp}F");
        return _temperatureSettings;
    }

    public async Task<SystemDiagnostics> GetDiagnosticsAsync()
    {
        _diagnostics.PowerUsageKwh = 1.2; // static mock value
        _logger.LogInformation("Retrieved system diagnostics");
        return await Task.FromResult(_diagnostics);
    }

    public async Task<List<InventoryItem>> GetInventoryAsync()
    {
        _logger.LogInformation($"Retrieved {_inventory.Count} inventory items");
        return await Task.FromResult(_inventory);
    }

    public async Task<List<Recipe>> GetRecipeSuggestionsAsync()
    {
        
        var recipes = new List<Recipe>();
        recipes.Add(new Recipe()
            {
                Name = "Chicken Caesar Salad",
                Ingredients = new() { "Chicken", "Lettuce", "Cheese", "Caesar Dressing" },
                Instructions = "1. Grill chicken and slice. 2. Wash and chop lettuce. 3. Mix with dressing and cheese. 4. Top with chicken.",
                PrepTimeMinutes = 15,
                CookTimeMinutes = 20,
                Servings = 4
            });
        recipes.Add(new Recipe()
            {
                Name = "Cheese Omelette",
                Ingredients = new() { "Eggs", "Cheese", "Butter", "Salt", "Pepper" },
                Instructions = "1. Beat eggs with salt and pepper. 2. Heat butter in pan. 3. Pour eggs and cook until set. 4. Add cheese and fold.",
                PrepTimeMinutes = 5,
                CookTimeMinutes = 5,
                Servings = 2
            });
        
        _logger.LogInformation($"Generated {recipes.Count} recipe suggestions");
        return recipes;
    }

}