using Microsoft.Extensions.Logging;
using MewAgent.Models;
using System.Collections.Concurrent;

namespace MewAgent.Services;

// manages internal timers for proactive agent behavior
public class TimerService : ITimerService, IDisposable
{
    private readonly ILogger<TimerService> _logger;
    private readonly ConcurrentDictionary<string, InternalTimer> _activeTimers;
    private readonly System.Threading.Timer _backgroundTimer;
    private readonly object _lockObject = new object();
    private bool _disposed = false;
    
    // delegate for executing LLM actions - will be set by MewAgentService
    public Func<string, Task<string>>? LLMInvoker { get; set; }
    
    // delegate for executing MCP tools - will be set by MewAgentService  
    public Func<string, Dictionary<string, object>, Task<string>>? ToolInvoker { get; set; }
    
    // delegate for showing messages to user - will be set by console app
    public Action<string>? MessageDisplayer { get; set; }

    public event EventHandler<TimerExpiredEventArgs>? TimerExpired;
    public event EventHandler<TimerActionResult>? ActionCompleted;

    public TimerService(ILogger<TimerService> logger)
    {
        _logger = logger;
        _activeTimers = new ConcurrentDictionary<string, InternalTimer>();
        
        // background timer that checks for expired timers every 5 seconds
        _backgroundTimer = new System.Threading.Timer(CheckExpiredTimers, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        
        _logger.LogInformation("TimerService initialized with background processing");
    }

    public async Task<string> SetTimerAsync(string name, TimeSpan duration, TimerAction action, string? description = null)
    {
        var expiresAt = DateTime.UtcNow.Add(duration);
        return await SetTimerAsync(name, expiresAt, action, description);
    }

    public async Task<string> SetTimerAsync(string name, DateTime expiresAt, TimerAction action, string? description = null)
    {
        var timer = new InternalTimer
        {
            Name = name,
            Description = description ?? string.Empty,
            ExpiresAt = expiresAt,
            Action = action
        };

        _activeTimers[timer.Id] = timer;
        
        _logger.LogInformation("Set timer '{Name}' (ID: {Id}) to expire at {ExpiresAt}", 
            name, timer.Id, expiresAt.ToString("yyyy-MM-dd HH:mm:ss"));
            
        return await Task.FromResult(timer.Id);
    }

    public async Task<bool> CancelTimerAsync(string timerId)
    {
        if (_activeTimers.TryRemove(timerId, out var timer))
        {
            timer.Status = TimerStatus.Cancelled;
            _logger.LogInformation("Cancelled timer '{Name}' (ID: {Id})", timer.Name, timerId);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> PauseTimerAsync(string timerId)
    {
        if (_activeTimers.TryGetValue(timerId, out var timer))
        {
            timer.Status = TimerStatus.Paused;
            _logger.LogInformation("Paused timer '{Name}' (ID: {Id})", timer.Name, timerId);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }

    public async Task<bool> ResumeTimerAsync(string timerId)
    {
        if (_activeTimers.TryGetValue(timerId, out var timer) && timer.Status == TimerStatus.Paused)
        {
            timer.Status = TimerStatus.Active;
            _logger.LogInformation("Resumed timer '{Name}' (ID: {Id})", timer.Name, timerId);
            return await Task.FromResult(true);
        }
        return await Task.FromResult(false);
    }

    public async Task<InternalTimer?> GetTimerAsync(string timerId)
    {
        _activeTimers.TryGetValue(timerId, out var timer);
        return await Task.FromResult(timer);
    }

    public async Task<List<InternalTimer>> GetActiveTimersAsync()
    {
        var activeTimers = _activeTimers.Values
            .Where(t => t.Status == TimerStatus.Active)
            .OrderBy(t => t.ExpiresAt)
            .ToList();
        return await Task.FromResult(activeTimers);
    }

    public async Task<List<InternalTimer>> GetTimersByNameAsync(string name)
    {
        var timers = _activeTimers.Values
            .Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return await Task.FromResult(timers);
    }

    // convenience methods
    public async Task<string> SetDelayedLLMAsync(string prompt, TimeSpan delay, string? description = null)
    {
        var action = new TimerAction
        {
            Type = TimerActionType.SmartTask,
            OriginalUserRequest = prompt,
            RequestedAt = DateTime.Now
        };
        
        return await SetTimerAsync($"DelayedLLM", delay, action, description ?? $"Delayed LLM call: {prompt}");
    }

    public async Task<string> SetReminderAsync(string message, TimeSpan delay, string? description = null)
    {
        var action = new TimerAction
        {
            Type = TimerActionType.SmartTask,
            OriginalUserRequest = message,
            RequestedAt = DateTime.Now
        };
        
        return await SetTimerAsync("Reminder", delay, action, description ?? $"Reminder: {message}");
    }

    public async Task<string> SetRecurringReminderAsync(string message, TimeSpan interval, int maxCount = 5)
    {
        var timer = new InternalTimer
        {
            Name = "RecurringReminder",
            Description = $"Recurring reminder: {message}",
            ExpiresAt = DateTime.UtcNow.Add(interval),
            IsRecurring = true,
            RecurringInterval = interval,
            MaxExecutions = maxCount,
            Action = new TimerAction
            {
                Type = TimerActionType.SmartTask,
                OriginalUserRequest = message,
                RequestedAt = DateTime.Now
            }
        };

        _activeTimers[timer.Id] = timer;
        _logger.LogInformation("Set recurring reminder '{Message}' every {Interval} for {MaxCount} times", 
            message, interval, maxCount);
            
        return await Task.FromResult(timer.Id);
    }

    // proactive scenario helpers
    public async Task<List<string>> SetEntertainmentTimersAsync(TimeSpan duration)
    {
        var timerIds = new List<string>();
        var endTime = DateTime.UtcNow.Add(duration);
        
        // set various entertainment timers throughout the duration
        var intervals = new[]
        {
            (TimeSpan.FromMinutes(15), "Tell me an interesting fact!"),
            (TimeSpan.FromMinutes(30), "How about a joke to lighten the mood?"),
            (TimeSpan.FromMinutes(45), "Let me suggest some music for you!"),
            (TimeSpan.FromMinutes(60), "Would you like a fun trivia question?"),
            (TimeSpan.FromMinutes(90), "How about we play a word game?")
        };

        foreach (var (interval, prompt) in intervals)
        {
            if (DateTime.UtcNow.Add(interval) <= endTime)
            {
                var timerId = await SetDelayedLLMAsync(prompt, interval, $"Entertainment: {prompt}");
                timerIds.Add(timerId);
            }
        }

        _logger.LogInformation("Set {Count} entertainment timers for {Duration}", timerIds.Count, duration);
        return timerIds;
    }

    public async Task<List<string>> SetCookingTimersAsync(string recipeName, List<(string step, TimeSpan duration)> steps)
    {
        var timerIds = new List<string>();
        var currentTime = DateTime.UtcNow;

        foreach (var (step, duration) in steps)
        {
            currentTime = currentTime.Add(duration);
            var action = new TimerAction
            {
                Type = TimerActionType.SmartTask,
                OriginalUserRequest = $"Cooking step completed for {recipeName}: {step}. What's the next step?",
                RequestedAt = DateTime.Now
            };

            var timer = new InternalTimer
            {
                Name = $"Cooking_{recipeName}",
                Description = $"Cooking step: {step}",
                ExpiresAt = currentTime,
                Action = action
            };

            _activeTimers[timer.Id] = timer;
            timerIds.Add(timer.Id);
        }

        _logger.LogInformation("Set {Count} cooking timers for recipe '{Recipe}'", steps.Count, recipeName);
        return timerIds;
    }

    // background processing
    private async void CheckExpiredTimers(object? state)
    {
        if (_disposed) return;

        try
        {
            var now = DateTime.UtcNow;
            var expiredTimers = _activeTimers.Values
                .Where(t => t.Status == TimerStatus.Active && t.ExpiresAt <= now)
                .ToList();

            foreach (var timer in expiredTimers)
            {
                await ExecuteTimerAsync(timer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking expired timers");
        }
    }

    private async Task ExecuteTimerAsync(InternalTimer timer)
    {
        try
        {
            _logger.LogInformation("Executing timer '{Name}' (ID: {Id})", timer.Name, timer.Id);
            
            // fire timer expired event
            TimerExpired?.Invoke(this, new TimerExpiredEventArgs { Timer = timer });

            // execute the timer action
            var result = await ExecuteTimerActionAsync(timer.Action);
            
            // increment execution count
            timer.ExecutionCount++;
            
            // handle recurring timers
            if (timer.IsRecurring && timer.ExecutionCount < timer.MaxExecutions && timer.RecurringInterval.HasValue)
            {
                timer.ExpiresAt = DateTime.UtcNow.Add(timer.RecurringInterval.Value);
                _logger.LogInformation("Recurring timer '{Name}' rescheduled for {ExpiresAt}", 
                    timer.Name, timer.ExpiresAt);
            }
            else
            {
                // timer is done, remove it
                timer.Status = TimerStatus.Expired;
                _activeTimers.TryRemove(timer.Id, out _);
                _logger.LogInformation("Timer '{Name}' completed and removed", timer.Name);
            }

            // fire action completed event
            ActionCompleted?.Invoke(this, result);
            
            // handle follow-up timers
            if (timer.Action.FollowUpTimers != null)
            {
                foreach (var followUp in timer.Action.FollowUpTimers)
                {
                    await SetTimerAsync(followUp.Name, followUp.DelayFromNow, followUp.Action);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing timer '{Name}' (ID: {Id})", timer.Name, timer.Id);
            
            ActionCompleted?.Invoke(this, new TimerActionResult
            {
                Success = false,
                Error = ex,
                Message = $"Failed to execute timer: {ex.Message}"
            });
        }
    }

    private async Task<TimerActionResult> ExecuteTimerActionAsync(TimerAction action)
    {
        var result = new TimerActionResult { Success = true };

        try
        {
            switch (action.Type)
            {
                case TimerActionType.InvokeLLM:
                    if (LLMInvoker != null && !string.IsNullOrEmpty(action.LLMPrompt))
                    {
                        result.LLMResponse = await LLMInvoker(action.LLMPrompt);
                        result.Message = "LLM invoked successfully";
                        
                        // show LLM response to user
                        if (MessageDisplayer != null && !string.IsNullOrEmpty(result.LLMResponse))
                        {
                            MessageDisplayer($"\nTimer Alert: {result.LLMResponse}");
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "LLM invoker not configured or prompt empty";
                    }
                    break;

                case TimerActionType.ShowMessage:
                    if (MessageDisplayer != null && !string.IsNullOrEmpty(action.Message))
                    {
                        MessageDisplayer($"\nTimer Alert: {action.Message}");
                        result.Message = "Message displayed";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Message displayer not configured or message empty";
                    }
                    break;

                case TimerActionType.ExecuteTool:
                    if (ToolInvoker != null && !string.IsNullOrEmpty(action.ToolName))
                    {
                        var toolResult = await ToolInvoker(action.ToolName, action.ToolParameters ?? new Dictionary<string, object>());
                        result.Message = $"Tool '{action.ToolName}' executed: {toolResult}";
                        
                        if (MessageDisplayer != null)
                        {
                            MessageDisplayer($"\nTimer Tool Result: {toolResult}");
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Tool invoker not configured or tool name empty";
                    }
                    break;

                case TimerActionType.SmartTask:
                    if (LLMInvoker != null && !string.IsNullOrEmpty(action.OriginalUserRequest))
                    {
                        var contextPrompt = $"The user previously asked: \"{action.OriginalUserRequest}\" at {action.RequestedAt:HH:mm:ss}. " +
                                          $"The timer has now expired. Please help them with their original request.";
                        
                        result.LLMResponse = await LLMInvoker(contextPrompt);
                        result.Message = "Smart task executed via LLM";
                        
                        // show response to user
                        if (MessageDisplayer != null && !string.IsNullOrEmpty(result.LLMResponse))
                        {
                            MessageDisplayer($"\nTimer Alert: {result.LLMResponse}");
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "LLM invoker not configured or original request missing";
                    }
                    break;

                case TimerActionType.PlaySound:
                    // for now, just show a message since we don't have audio support
                    if (MessageDisplayer != null)
                    {
                        MessageDisplayer("\nTimer Alert: *BEEP*!");
                        result.Message = "Sound notification sent";
                    }
                    break;

                default:
                    result.Success = false;
                    result.Message = $"Unknown timer action type: {action.Type}";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex;
            result.Message = $"Error executing timer action: {ex.Message}";
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _backgroundTimer?.Dispose();
            _activeTimers.Clear();
            _logger.LogInformation("TimerService disposed");
        }
    }
}