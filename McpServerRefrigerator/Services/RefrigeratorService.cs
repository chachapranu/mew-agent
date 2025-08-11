using Shared;

namespace McpServerRefrigerator.Services;

public class RefrigeratorService
{
    private readonly ILogger<RefrigeratorService> _logger;
    private readonly Random _random = new Random();
    
    private TemperatureSettings _temperatureSettings = new();
    private SystemDiagnostics _diagnostics = new();
    // Default fridge inventory - probably should load from database later
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
        await SimulateDelay();
        _logger.LogInformation("Getting temperature settings");
        return _temperatureSettings;
    }

    public async Task<TemperatureSettings> SetTemperatureAsync(double? fridgeTemp, double? freezerTemp)
    {
        await SimulateDelay();
        
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
        await SimulateDelay();
        
        // simulate some random stuff happening
        _diagnostics.PowerUsageKwh = Math.Round(1.0 + _random.NextDouble() * 0.5, 2);
        _diagnostics.FilterDaysRemaining = Math.Max(0, _diagnostics.FilterDaysRemaining - 1);
        
        if (_diagnostics.FilterDaysRemaining < 30)
        {
            if (!_diagnostics.ActiveAlerts.Contains("Filter replacement needed soon"))
                _diagnostics.ActiveAlerts.Add("Filter replacement needed soon");
        }
        
        _logger.LogInformation("Retrieved system diagnostics");
        return _diagnostics;
    }

    public async Task<List<InventoryItem>> GetInventoryAsync()
    {
        await SimulateDelay();
        _logger.LogInformation($"Retrieved {_inventory.Count} inventory items");
        return _inventory;
    }

    public async Task<List<Recipe>> GetRecipeSuggestionsAsync()
    {
        await SimulateDelay();
        
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

    private async Task SimulateDelay()
    {
        // add some delay to make it feel real
        var delay = _random.Next(100, 500);
        await Task.Delay(delay);
    }
}