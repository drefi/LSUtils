namespace LSUtils.BehaviorTree;

/// <summary>
/// Execution status returned by behavior tree nodes and tasks.
/// </summary>
public enum BTStatus {
    /// <summary>Task has not yet been executed, or has been reset via abort.</summary>
    FRESH = 0,

    /// <summary>The task completed its work successfully.</summary>
    SUCCESS,

    /// <summary>The task failed or its condition was not met.</summary>
    FAILURE,

    /// <summary>The task is still executing and should be ticked again next frame.</summary>
    RUNNING,
}
