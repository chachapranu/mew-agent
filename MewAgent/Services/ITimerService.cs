using MewAgent.Models;

namespace MewAgent.Services;

// interface for managing internal timers that trigger proactive actions
public interface ITimerService
{
    // timer management
    Task<string> SetTimerAsync(string name, TimeSpan duration, TimerAction action, string? description = null);
    Task<string> SetTimerAsync(string name, DateTime expiresAt, TimerAction action, string? description = null);
    Task<bool> CancelTimerAsync(string timerId);
    Task<bool> PauseTimerAsync(string timerId);
    Task<bool> ResumeTimerAsync(string timerId);
    
    // timer queries
    Task<InternalTimer?> GetTimerAsync(string timerId);
    Task<List<InternalTimer>> GetActiveTimersAsync();
    Task<List<InternalTimer>> GetTimersByNameAsync(string name);
    
    // convenience methods for common scenarios
    Task<string> SetDelayedLLMAsync(string prompt, TimeSpan delay, string? description = null);
    Task<string> SetReminderAsync(string message, TimeSpan delay, string? description = null);
    Task<string> SetRecurringReminderAsync(string message, TimeSpan interval, int maxCount = 5);
    
    // proactive scenario helpers
    Task<List<string>> SetEntertainmentTimersAsync(TimeSpan duration); // for "entertain me for X hours"
    Task<List<string>> SetCookingTimersAsync(string recipeName, List<(string step, TimeSpan duration)> steps);
    
    // events for timer expiration
    event EventHandler<TimerExpiredEventArgs>? TimerExpired;
    event EventHandler<TimerActionResult>? ActionCompleted;
}

public class TimerExpiredEventArgs : EventArgs
{
    public InternalTimer Timer { get; set; } = null!;
    public DateTime ExpiredAt { get; set; } = DateTime.UtcNow;
}