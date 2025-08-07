namespace Shared;

public class TemperatureSettings
{
    public double FridgeTemp { get; set; } = 37.0; // Fahrenheit
    public double FreezerTemp { get; set; } = 0.0; // Fahrenheit
    public string Mode { get; set; } = "Normal"; // Normal, PowerSave, QuickCool
}

public class SystemDiagnostics
{
    public string Status { get; set; } = "Healthy";
    public int FilterDaysRemaining { get; set; } = 90;
    public double PowerUsageKwh { get; set; } = 1.2;
    public List<string> ActiveAlerts { get; set; } = new();
    public DateTime LastMaintenance { get; set; } = DateTime.Now.AddDays(-30);
}

public class InventoryItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime? ExpirationDate { get; set; }
}

public class Recipe
{
    public string Name { get; set; } = string.Empty;
    public List<string> Ingredients { get; set; } = new();
    public string Instructions { get; set; } = string.Empty;
    public int PrepTimeMinutes { get; set; }
    public int CookTimeMinutes { get; set; }
    public int Servings { get; set; }
}