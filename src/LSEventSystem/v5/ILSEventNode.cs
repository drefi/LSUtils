namespace LSUtils.EventSystem;

public interface ILSEventNode {
    string NodeID { get; }  // Used as path identifier for navigation
    LSPriority Priority { get; }
    LSEventCondition Conditions { get; }
    int ExecutionCount { get; } // Number of times this node has been executed
    ILSEventNode Clone();
    LSEventProcessStatus Process(LSEventProcessContext context);
}
