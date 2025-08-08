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
        
        await agent.InitializeAsync();
        
        Console.WriteLine("🐱 Mew Agent - Smart Refrigerator Assistant");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        
        ShowHelp();
        
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nYou: ");
            Console.ResetColor();
            
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;
            
            if (input.StartsWith("/"))
            {
                if (!await HandleCommand(input, agent))
                    break;
                continue;
            }
            
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
                services.AddSingleton<SimpleMcpService>();
                services.AddSingleton<MewAgentService>();
                
                services.AddLogging(builder =>
                {
                    builder.ClearProviders()
                           .AddConsole()
                           .SetMinimumLevel(LogLevel.Information);
                });
            });
    
    static void ShowHelp()
    {
        Console.WriteLine("📚 Available Commands:");
        Console.WriteLine("  /help     - Show this help message");
        Console.WriteLine("  /tools    - List available refrigerator tools");
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
    
    static async Task<bool> HandleCommand(string command, MewAgentService agent)
    {
        switch (command.ToLower())
        {
            case "/help":
                ShowHelp();
                return true;
                
            case "/tools":
                Console.WriteLine("\n🔧 Available Tools:");
                Console.WriteLine("  • GetTemperature - Get current refrigerator and freezer temperature settings");
                Console.WriteLine("  • SetTemperature - Set refrigerator and/or freezer temperature");
                Console.WriteLine("  • GetDiagnostics - Get system health and maintenance information");
                Console.WriteLine("  • GetInventory - Get current food inventory in the refrigerator");
                Console.WriteLine("  • GetRecipeSuggestions - Get recipe suggestions based on available ingredients");
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