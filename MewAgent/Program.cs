using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MewAgent.Services;

namespace MewAgent;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var agent = host.Services.GetRequiredService<MewAgentService>();
        var hybridMcp = host.Services.GetRequiredService<HybridMcpService>();
        
        // Initialize the agent with MCP plugin
        await agent.InitializeAsync();
        
        Console.WriteLine("🐱 Mew Agent - Smart Refrigerator Assistant");
        Console.WriteLine("===========================================");
        Console.WriteLine();
        
        // Check server connection
        Console.Write("Connecting to MCP/HTTP Server... ");
        if (await hybridMcp.CheckConnectionAsync())
        {
            Console.WriteLine("✅ Connected!");
            
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("❌ Failed!");
            Console.WriteLine("⚠️  Warning: MCP Server is not available. Some features may not work.");
            Console.WriteLine("   Make sure the MCP Server is running on http://localhost:5100");
            Console.WriteLine();
        }
        
        // Display help
        ShowHelp();
        
        // Main conversation loop
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nYou: ");
            Console.ResetColor();
            
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;
            
            // Handle debug commands
            if (input.StartsWith("/"))
            {
                if (!await HandleCommand(input, agent, hybridMcp))
                    break;
                continue;
            }
            
            // Process user message
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("\nMew: ");
            Console.ResetColor();
            
            var response = await agent.ProcessMessageAsync(input);
            Console.WriteLine(response);
        }
        
        Console.WriteLine("\n👋 Goodbye! Thanks for using Mew Agent.");
    }
    
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Add HTTP client for MCP fallback
                services.AddHttpClient<McpClientService>();
                
                // Add services
                services.AddSingleton<HybridMcpService>();
                services.AddSingleton<MewAgentService>();
                
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                           .AddConsole()
                           .SetMinimumLevel(LogLevel.Warning);
                });
            });
    
    static void ShowHelp()
    {
        Console.WriteLine("📚 Available Commands:");
        Console.WriteLine("  /help     - Show this help message");
        Console.WriteLine("  /tools    - List available refrigerator tools");
        Console.WriteLine("  /status   - Check system status");
        Console.WriteLine("  /memory   - Show conversation memory usage");
        Console.WriteLine("  /clear    - Clear conversation history");
        Console.WriteLine("  /quit     - Exit the application");
        Console.WriteLine();
        Console.WriteLine("💡 Try asking me:");
        Console.WriteLine("  - What's the current temperature?");
        Console.WriteLine("  - What food do I have?");
        Console.WriteLine("  - Suggest a recipe for dinner");
        Console.WriteLine("  - Check the system diagnostics");
        Console.WriteLine("  - I want to cook for 2 hours");
    }
    
    static async Task<bool> HandleCommand(string command, MewAgentService agent, HybridMcpService hybridMcp)
    {
        switch (command.ToLower())
        {
            case "/help":
                ShowHelp();
                return true;
                
            case "/tools":
                Console.WriteLine("\n🔧 Available Tools:");
                Console.WriteLine("  • GetTemperature (Temperature Control)");
                Console.WriteLine("    Get current refrigerator and freezer temperature settings");
                Console.WriteLine("  • SetTemperature (Temperature Control)");
                Console.WriteLine("    Set refrigerator and/or freezer temperature");
                Console.WriteLine("  • GetDiagnostics (Diagnostics)");
                Console.WriteLine("    Get system health and maintenance information");
                Console.WriteLine("  • GetInventory (Inventory)");
                Console.WriteLine("    Get current food inventory in the refrigerator");
                Console.WriteLine("  • GetRecipeSuggestions (Recipe Assistant)");
                Console.WriteLine("    Get recipe suggestions based on available ingredients");
                return true;
                
            case "/status":
                Console.WriteLine("\n📊 System Status:");
                var connected = await hybridMcp.CheckConnectionAsync();
                Console.WriteLine($"  Server: {(connected ? "✅ Connected" : "❌ Disconnected")}");
                Console.WriteLine($"  Memory Usage: {agent.GetHistoryCount()} messages");
                return true;
                
            case "/memory":
                Console.WriteLine($"\n🧠 Conversation Memory: {agent.GetHistoryCount()} messages");
                return true;
                
            case "/clear":
                agent.ClearHistory();
                Console.WriteLine("✨ Conversation history cleared!");
                return true;
                
            case "/quit":
            case "/exit":
                return false;
                
            default:
                Console.WriteLine($"❓ Unknown command: {command}");
                Console.WriteLine("   Type /help for available commands");
                return true;
        }
    }
}