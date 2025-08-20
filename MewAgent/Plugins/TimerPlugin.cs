using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using MewAgent.Services;
using MewAgent.Models;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace MewAgent.Plugins;

// semantic kernel plugin that exposes timer functionality to the LLM
public class TimerPlugin
{
    private readonly ITimerService _timerService;
    private readonly ILogger<TimerPlugin> _logger;

    public TimerPlugin(ITimerService timerService, ILogger<TimerPlugin> logger)
    {
        _timerService = timerService;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Set a timer to perform an action after a specified duration. Use this for delayed responses, reminders, or scheduled actions.")]
    public async Task<string> SetTimer(
        [Description("Name/description of the timer")] string name,
        [Description("Duration like '5 minutes', '2 hours', '30 seconds', '1 hour 30 minutes'")] string duration,
        [Description("Action type: 'llm_call', 'reminder', 'tool_call', 'message'")] string actionType,
        [Description("The prompt to send to LLM, message to show, or tool to call")] string actionContent,
        [Description("Optional tool parameters as JSON if actionType is 'tool_call'")] string? toolParameters = null)
    {
        try
        {
            var timeSpan = ParseDuration(duration);
            if (timeSpan == TimeSpan.Zero)
            {
                return $"Error: Could not parse duration '{duration}'. Use formats like '5 minutes', '2 hours', '30 seconds'";
            }

            var action = CreateTimerAction(actionType, actionContent, toolParameters);
            var timerId = await _timerService.SetTimerAsync(name, timeSpan, action);
            
            _logger.LogInformation("Timer set via LLM: {Name} for {Duration}", name, timeSpan);
            
            return $"Timer '{name}' set for {duration} (ID: {timerId[..8]}). " +
                   $"It will {GetActionDescription(action)} at {DateTime.Now.Add(timeSpan):HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting timer via LLM");
            return $"Error: Failed to set timer: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Set a timer to provide a delayed LLM response. Perfect for 'give me X in Y minutes' scenarios.")]
    public async Task<string> SetDelayedResponse(
        [Description("What to provide/generate (e.g., 'recipe for coffee', 'joke', 'interesting fact')")] string responseContent,
        [Description("Delay duration like '2 minutes', '1 hour', '30 seconds'")] string delay,
        [Description("Optional context or specific requirements")] string? context = null)
    {
        try
        {
            var timeSpan = ParseDuration(delay);
            if (timeSpan == TimeSpan.Zero)
            {
                return $"Error: Could not parse delay '{delay}'";
            }

            var prompt = $"Provide {responseContent}";
            if (!string.IsNullOrEmpty(context))
            {
                prompt += $". Context: {context}";
            }

            var timerId = await _timerService.SetDelayedLLMAsync(prompt, timeSpan, 
                $"Delayed response: {responseContent}");
            
            return $"I'll provide {responseContent} in {delay} at {DateTime.Now.Add(timeSpan):HH:mm:ss}. " +
                   $"Timer ID: {timerId[..8]}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting delayed response");
            return $"Error: Failed to set delayed response: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Set a reminder timer to show a message at a specific time.")]
    public async Task<string> SetReminder(
        [Description("The reminder message to show")] string message,
        [Description("When to remind (e.g., '10 minutes', '1 hour', '5 seconds')")] string when,
        [Description("Optional recurring interval like 'every 30 minutes' (max 5 times)")] string? recurring = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(recurring) && recurring.StartsWith("every "))
            {
                var intervalStr = recurring.Substring(6); // remove "every "
                var interval = ParseDuration(intervalStr);
                if (interval != TimeSpan.Zero)
                {
                    var recurringTimerId = await _timerService.SetRecurringReminderAsync(message, interval);
                    return $"Recurring reminder set: '{message}' every {intervalStr} (5 times max). ID: {recurringTimerId[..8]}";
                }
            }

            var timeSpan = ParseDuration(when);
            if (timeSpan == TimeSpan.Zero)
            {
                return $"Error: Could not parse time '{when}'";
            }

            var timerId = await _timerService.SetReminderAsync(message, timeSpan);
            
            return $"Reminder set for {when}: '{message}' at {DateTime.Now.Add(timeSpan):HH:mm:ss}. " +
                   $"ID: {timerId[..8]}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting reminder");
            return $"Error: Failed to set reminder: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Activate entertainment mode with multiple proactive interactions over a duration.")]
    public async Task<string> SetEntertainmentMode(
        [Description("How long to entertain (e.g., '2 hours', '30 minutes', '1 hour')")] string duration)
    {
        try
        {
            var timeSpan = ParseDuration(duration);
            if (timeSpan == TimeSpan.Zero)
            {
                return $"Error: Could not parse duration '{duration}'";
            }

            if (timeSpan.TotalMinutes < 10)
            {
                return "Warning: Entertainment mode works best for durations of 10 minutes or more.";
            }

            var timerIds = await _timerService.SetEntertainmentTimersAsync(timeSpan);
            
            return $"Entertainment mode activated for {duration}! " +
                   $"I've set {timerIds.Count} proactive interactions. " +
                   $"I'll periodically entertain you with jokes, facts, music suggestions, and games until {DateTime.Now.Add(timeSpan):HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting entertainment mode");
            return $"Error: Failed to activate entertainment mode: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Cancel a timer by its name or ID.")]
    public async Task<string> CancelTimer(
        [Description("Timer name or ID to cancel")] string timerIdentifier)
    {
        try
        {
            // try to find timer by ID first
            var timer = await _timerService.GetTimerAsync(timerIdentifier);
            if (timer != null)
            {
                var success = await _timerService.CancelTimerAsync(timerIdentifier);
                return success ? 
                    $"Cancelled timer '{timer.Name}'" : 
                    $"Error: Failed to cancel timer '{timer.Name}'";
            }

            // try to find by name
            var timersByName = await _timerService.GetTimersByNameAsync(timerIdentifier);
            if (timersByName.Any())
            {
                var cancelledCount = 0;
                foreach (var t in timersByName)
                {
                    if (await _timerService.CancelTimerAsync(t.Id))
                        cancelledCount++;
                }
                return $"Cancelled {cancelledCount} timer(s) named '{timerIdentifier}'";
            }

            return $"Error: No timer found with name or ID '{timerIdentifier}'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling timer");
            return $"Error: Failed to cancel timer: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("List all active timers with their details.")]
    public async Task<string> ListActiveTimers()
    {
        try
        {
            var activeTimers = await _timerService.GetActiveTimersAsync();
            
            if (!activeTimers.Any())
            {
                return "No active timers currently set.";
            }

            var result = $"Active Timers ({activeTimers.Count}):\n";
            foreach (var timer in activeTimers)
            {
                var timeLeft = timer.ExpiresAt - DateTime.UtcNow;
                var timeLeftStr = timeLeft.TotalSeconds > 0 ? 
                    FormatTimeSpan(timeLeft) : "Overdue";
                    
                result += $"• {timer.Name} - {timeLeftStr} (ID: {timer.Id[..8]})\n";
                result += $"  Action: {GetActionDescription(timer.Action)}\n";
                
                if (timer.IsRecurring)
                {
                    result += $"  Recurring: {timer.ExecutionCount}/{timer.MaxExecutions}\n";
                }
            }
            
            return result.TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing timers");
            return $"Error: Failed to list timers: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Set up cooking guidance timers for a recipe with multiple steps.")]
    public async Task<string> SetCookingGuide(
        [Description("Name of the recipe")] string recipeName,
        [Description("JSON array of cooking steps with durations, e.g. [{'step':'Preheat oven','duration':'10 minutes'},{'step':'Bake','duration':'25 minutes'}]")] string stepsJson)
    {
        try
        {
            var steps = JsonSerializer.Deserialize<List<CookingStep>>(stepsJson);
            if (steps == null || !steps.Any())
            {
                return "Error: Invalid or empty cooking steps. Please provide steps in JSON format.";
            }

            var cookingSteps = new List<(string step, TimeSpan duration)>();
            foreach (var step in steps)
            {
                var duration = ParseDuration(step.Duration);
                if (duration == TimeSpan.Zero)
                {
                    return $"Error: Could not parse duration '{step.Duration}' for step '{step.Step}'";
                }
                cookingSteps.Add((step.Step, duration));
            }

            var timerIds = await _timerService.SetCookingTimersAsync(recipeName, cookingSteps);
            
            var totalTime = cookingSteps.Sum(s => s.duration.TotalMinutes);
            return $"Cooking guide set for '{recipeName}' with {cookingSteps.Count} steps. " +
                   $"Total cooking time: {FormatTimeSpan(TimeSpan.FromMinutes(totalTime))}. " +
                   $"I'll guide you through each step!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cooking guide");
            return $"Error: Failed to set cooking guide: {ex.Message}";
        }
    }

    // helper methods
    private TimeSpan ParseDuration(string duration)
    {
        try
        {
            // normalize the input
            duration = duration.ToLowerInvariant().Trim();
            
            // patterns for different time formats
            var patterns = new[]
            {
                @"(\d+)\s*hour[s]?\s*(\d+)\s*minute[s]?", // "2 hours 30 minutes"
                @"(\d+)\s*minute[s]?\s*(\d+)\s*second[s]?", // "5 minutes 30 seconds"  
                @"(\d+)\s*hour[s]?", // "2 hours"
                @"(\d+)\s*minute[s]?", // "30 minutes"
                @"(\d+)\s*second[s]?", // "45 seconds"
                @"(\d+)\s*h", // "2h"
                @"(\d+)\s*m", // "30m" 
                @"(\d+)\s*s", // "45s"
            };

            // try compound formats first
            var hourMinuteMatch = Regex.Match(duration, patterns[0]);
            if (hourMinuteMatch.Success)
            {
                var hours = int.Parse(hourMinuteMatch.Groups[1].Value);
                var minutes = int.Parse(hourMinuteMatch.Groups[2].Value);
                return TimeSpan.FromMinutes(hours * 60 + minutes);
            }

            var minuteSecondMatch = Regex.Match(duration, patterns[1]);
            if (minuteSecondMatch.Success)
            {
                var minutes = int.Parse(minuteSecondMatch.Groups[1].Value);
                var seconds = int.Parse(minuteSecondMatch.Groups[2].Value);
                return TimeSpan.FromSeconds(minutes * 60 + seconds);
            }

            // try single unit formats
            for (int i = 2; i < patterns.Length; i++)
            {
                var match = Regex.Match(duration, patterns[i]);
                if (match.Success)
                {
                    var value = int.Parse(match.Groups[1].Value);
                    return i switch
                    {
                        2 or 5 => TimeSpan.FromHours(value), // hours
                        3 or 6 => TimeSpan.FromMinutes(value), // minutes
                        4 or 7 => TimeSpan.FromSeconds(value), // seconds
                        _ => TimeSpan.Zero
                    };
                }
            }

            return TimeSpan.Zero;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private TimerAction CreateTimerAction(string actionType, string actionContent, string? toolParameters)
    {
        var action = new TimerAction();
        
        switch (actionType.ToLowerInvariant())
        {
            case "llm_call":
            case "llm":
                action.Type = TimerActionType.InvokeLLM;
                action.LLMPrompt = actionContent;
                break;
                
            case "reminder":
            case "message":
                // for simple reminders, just show a message
                if (IsSimpleReminder(actionContent))
                {
                    action.Type = TimerActionType.ShowMessage;
                    action.Message = actionContent;
                }
                else
                {
                    // for complex reminders that might need tools, use smart task
                    action.Type = TimerActionType.SmartTask;
                    action.OriginalUserRequest = actionContent;
                    action.RequestedAt = DateTime.Now;
                }
                break;
                
            case "tool_call":
            case "tool":
                // Instead of direct tool execution, use SmartTask to let LLM handle tool calling and formatting
                action.Type = TimerActionType.SmartTask;
                action.OriginalUserRequest = ConvertToolNameToNaturalRequest(actionContent);
                action.RequestedAt = DateTime.Now;
                break;
                
            default:
                // for generic requests, use smart task to replay original context
                action.Type = TimerActionType.SmartTask;
                action.OriginalUserRequest = actionContent;
                action.RequestedAt = DateTime.Now;
                break;
        }
        
        return action;
    }
    
    private bool IsSimpleReminder(string content)
    {
        // simple reminders are just text messages that don't require tools
        var lowerContent = content.ToLowerInvariant();
        return !lowerContent.Contains("temperature") && 
               !lowerContent.Contains("inventory") && 
               !lowerContent.Contains("food") && 
               !lowerContent.Contains("recipe") && 
               !lowerContent.Contains("diagnostic") &&
               !lowerContent.Contains("check") &&
               !lowerContent.Contains("get") &&
               !lowerContent.Contains("what");
    }


    private string ConvertToolNameToNaturalRequest(string toolName)
    {
        // convert tool names to natural language requests that the LLM can understand
        var normalized = toolName.ToLowerInvariant().Trim();
        
        return normalized switch
        {
            "get temperature" => "What is the current refrigerator temperature?",
            "check temperature" => "Check the refrigerator temperature",
            "temperature" => "Tell me the refrigerator temperature",
            "gettemperature" => "What is the current refrigerator temperature?",
            "get inventory" => "What food items are in the refrigerator?",
            "check inventory" => "Check the refrigerator inventory",
            "inventory" => "Show me what's in the refrigerator",
            "getinventory" => "What food items are in the refrigerator?",
            "get diagnostics" => "Check the refrigerator system diagnostics",
            "check diagnostics" => "Run refrigerator diagnostics",
            "diagnostics" => "Show refrigerator system status",
            "getdiagnostics" => "Check the refrigerator system diagnostics",
            "get recipes" => "Suggest recipes based on available ingredients",
            "get recipe suggestions" => "What recipes can I make with available ingredients?",
            "recipes" => "Give me recipe suggestions",
            "getrecipesuggestions" => "Suggest recipes based on available ingredients",
            "set temperature" => "Set the refrigerator temperature",
            "settemperature" => "Set the refrigerator temperature",
            _ => toolName // return original if no mapping found - assume it's already a natural request
        };
    }

    private string GetActionDescription(TimerAction action)
    {
        return action.Type switch
        {
            TimerActionType.InvokeLLM => $"call LLM with: '{action.LLMPrompt}'",
            TimerActionType.ShowMessage => $"show message: '{action.Message}'",
            TimerActionType.ExecuteTool => $"execute tool: '{action.ToolName}'",
            TimerActionType.SmartTask => $"handle request: '{action.OriginalUserRequest}'",
            TimerActionType.PlaySound => "play sound notification",
            _ => "perform custom action"
        };
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        else if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        else if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        else
            return $"{timeSpan.Seconds}s";
    }

    private class CookingStep
    {
        public string Step { get; set; } = "";
        public string Duration { get; set; } = "";
    }
}