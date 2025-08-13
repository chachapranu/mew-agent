namespace MewAgent.Models;

// represents an internal timer that can trigger various actions
public class InternalTimer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public TimeSpan Duration => ExpiresAt - CreatedAt;
    public TimerStatus Status { get; set; } = TimerStatus.Active;
    public TimerAction Action { get; set; } = new();
    public bool IsRecurring { get; set; } = false;
    public TimeSpan? RecurringInterval { get; set; }
    public int ExecutionCount { get; set; } = 0;
    public int MaxExecutions { get; set; } = 1;
}

public enum TimerStatus
{
    Active,
    Expired,
    Cancelled,
    Paused
}

// defines what action to take when timer expires
public class TimerAction
{
    public TimerActionType Type { get; set; }
    public string? LLMPrompt { get; set; }
    public string? Message { get; set; }
    public string? ToolName { get; set; }
    public Dictionary<string, object>? ToolParameters { get; set; }
    public List<FollowUpTimer>? FollowUpTimers { get; set; }
    public bool RequireUserPresence { get; set; } = true; // only execute if user is active
    
    // stores the original user request context for intelligent timer execution
    public string? OriginalUserRequest { get; set; }
    public DateTime? RequestedAt { get; set; }
}

public enum TimerActionType
{
    InvokeLLM,          // call LLM with specified prompt
    ShowMessage,        // display message to user
    ExecuteTool,        // call MCP tool
    PlaySound,          // audio notification
    SetFollowUpTimers,  // create additional timers
    SmartTask,          // replay original user request to LLM for intelligent execution
    Custom              // custom action handler
}

// allows chaining timers for complex scenarios
public class FollowUpTimer
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan DelayFromNow { get; set; }
    public TimerAction Action { get; set; } = new();
}

// result of timer action execution
public class TimerActionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? LLMResponse { get; set; }
    public Exception? Error { get; set; }
    public List<InternalTimer>? CreatedFollowUpTimers { get; set; }
}