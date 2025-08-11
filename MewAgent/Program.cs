using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MewAgent.Services;

namespace MewAgent;

class Program
{
    static async Task Main(string[] args)
    {
        // simple dependency injection setup without hosting
        var services = ConfigureServices();
        var serviceProvider = services.BuildServiceProvider();
        
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var agent = serviceProvider.GetRequiredService<MewAgentService>();
        
        logger.LogInformation("Starting Mew Agent...");
        
        try
        {
            await agent.InitializeAsync();
            await RunConsoleLoop(agent, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error occurred");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            serviceProvider.Dispose();
        }
    }
    
    static IServiceCollection ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        
        // register configuration
        services.AddSingleton<IConfiguration>(configuration);
        
        // register logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // register application services
        services.AddSingleton<ProperMcpClientService>();
        services.AddSingleton<MewAgentService>();
        
        return services;
    }
    
    static async Task RunConsoleLoop(MewAgentService agent, ILogger<Program> logger)
    {
        Console.Clear();
        Console.WriteLine("Mew Agent - Smart Refrigerator Assistant");
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
            
            // handle commands
            if (input.StartsWith("/"))
            {
                if (!HandleCommand(input, agent))
                    break;
                continue;
            }
            
            // process user message
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nMew: ");
                Console.ResetColor();
                
                var response = await agent.ProcessMessageAsync(input);
                Console.WriteLine(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }
        
        Console.WriteLine("\nGoodbye! Thanks for using Mew Agent.");
    }
    
    static void ShowHelp()
    {
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  /help     - Show this help message");
        Console.WriteLine("  /tools    - List available refrigerator tools");
        Console.WriteLine("  /memory   - Show conversation memory usage");
        Console.WriteLine("  /clear    - Clear conversation history");
        Console.WriteLine("  /quit     - Exit the application");
        Console.WriteLine();
        Console.WriteLine("Try asking me:");
        Console.WriteLine("  - What's the current temperature?");
        Console.WriteLine("  - What food do I have?");
        Console.WriteLine("  - Suggest a recipe for dinner");
        Console.WriteLine("  - Check the system diagnostics");
    }
    
    static bool HandleCommand(string command, MewAgentService agent)
    {
        switch (command.ToLower())
        {
            case "/help":
                ShowHelp();
                return true;
                
            case "/tools":
                Console.WriteLine("\nAvailable Tools:");
                Console.WriteLine("  - GetTemperature: Check refrigerator and freezer temperatures");
                Console.WriteLine("  - SetTemperature: Adjust temperature settings");
                Console.WriteLine("  - GetDiagnostics: View system health and maintenance info");
                Console.WriteLine("  - GetInventory: Check food inventory");
                Console.WriteLine("  - GetRecipeSuggestions: Get recipe ideas based on ingredients");
                return true;
                
            case "/memory":
                Console.WriteLine($"\nConversation Memory: {agent.GetHistoryCount()} messages");
                return true;
                
            case "/clear":
                agent.ClearHistory();
                Console.WriteLine("Conversation history cleared!");
                return true;
                
            case "/quit":
            case "/exit":
                return false;
                
            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type /help for available commands");
                return true;
        }
    }
}