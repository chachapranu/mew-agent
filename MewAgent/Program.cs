using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MewAgent.Services;
using MewAgent.Plugins;

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
            // Clean up MCP client
            var mcpClient = serviceProvider.GetService<McpClientService>();
            mcpClient?.Dispose();
            
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
        services.AddSingleton<McpClientService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<MewAgentService>();
        
        // register HttpClient for potential HTTP transport
        services.AddHttpClient();
        
        return services;
    }
    
    static async Task RunConsoleLoop(MewAgentService agent, ILogger<Program> logger)
    {
        Console.Clear();
        Console.WriteLine("Mew Agent - Smart Refrigerator Assistant");
        Console.WriteLine("=========================================");
        Console.WriteLine();
        
        // set up timer message displayer for console output
        agent.MessageDisplayer = (message) =>
        {
            // save current console color
            var currentColor = Console.ForegroundColor;
            
            // display timer message in yellow
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            
            // restore original color
            Console.ForegroundColor = currentColor;
        };
        
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
                if (!await HandleCommand(input, agent))
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
        Console.WriteLine("  /timers   - List active timers");
        Console.WriteLine("  /memory   - Show conversation memory usage");
        Console.WriteLine("  /clear    - Clear conversation history");
        Console.WriteLine("  /quit     - Exit the application");
        Console.WriteLine();
        Console.WriteLine("Try asking me:");
        Console.WriteLine("  - What's the current temperature?");
        Console.WriteLine("  - What food do I have?");
        Console.WriteLine("  - Suggest a recipe for dinner");
        Console.WriteLine("  - Give me a coffee recipe in 2 minutes");
        Console.WriteLine("  - Set a reminder to check the oven in 10 minutes");
        Console.WriteLine("  - Entertain me for the next hour");
    }
    
    static async Task<bool> HandleCommand(string command, MewAgentService agent)
    {
        switch (command.ToLower())
        {
            case "/help":
                ShowHelp();
                return await Task.FromResult(true);
                
            case "/tools":
                Console.WriteLine("\nAvailable Tools:");
                Console.WriteLine("  Refrigerator:");
                Console.WriteLine("    - GetTemperature: Check refrigerator and freezer temperatures");
                Console.WriteLine("    - SetTemperature: Adjust temperature settings");
                Console.WriteLine("    - GetDiagnostics: View system health and maintenance info");
                Console.WriteLine("    - GetInventory: Check food inventory");
                Console.WriteLine("    - GetRecipeSuggestions: Get recipe ideas based on ingredients");
                Console.WriteLine("  Timers:");
                Console.WriteLine("    - SetDelayedResponse: Get responses after a delay");
                Console.WriteLine("    - SetReminder: Set reminder messages");
                Console.WriteLine("    - SetEntertainmentMode: Proactive entertainment over time");
                Console.WriteLine("    - CancelTimer: Cancel active timers");
                return await Task.FromResult(true);
                
            case "/timers":
                {
                    var timers = await agent.GetActiveTimersAsync();
                    if (!timers.Any())
                    {
                        Console.WriteLine("\nNo active timers.");
                    }
                    else
                    {
                        Console.WriteLine($"\nActive Timers ({timers.Count}):");
                        foreach (var timer in timers)
                        {
                            var timeLeft = timer.ExpiresAt - DateTime.UtcNow;
                            var timeLeftStr = timeLeft.TotalSeconds > 0 ? 
                                $"{(int)timeLeft.TotalMinutes}m {timeLeft.Seconds}s" : "Overdue";
                            Console.WriteLine($"  • {timer.Name} - {timeLeftStr} (ID: {timer.Id[..8]})");
                        }
                    }
                    return await Task.FromResult(true);
                }
                
            case "/memory":
                Console.WriteLine($"\nConversation Memory: {agent.GetHistoryCount()} messages");
                return await Task.FromResult(true);
                
            case "/clear":
                agent.ClearHistory();
                Console.WriteLine("Conversation history cleared!");
                return await Task.FromResult(true);
                
            case "/quit":
            case "/exit":
                return await Task.FromResult(false);
                
            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type /help for available commands");
                return await Task.FromResult(true);
        }
    }
}