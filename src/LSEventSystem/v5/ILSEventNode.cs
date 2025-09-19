namespace LSUtils.EventSystem;

public interface ILSEventNode {
    string NodeID { get; }  // Used as path identifier for navigation
    LSPriority Priority { get; }
    // Conditions to be met to execute this node, a node that does not meet conditions should be skipped
    LSEventCondition Conditions { get; }
    int ExecutionCount { get; } // Number of times this node has been executed
    int Order { get; } // Order of execution among nodes, this is set during registration order (increased)
    ILSEventNode Clone();
    //LSEventProcessStatus Process(LSEventProcessContext context, params string[]? nodes);
    LSEventProcessStatus Process(LSEventProcessContext context);
    LSEventProcessStatus GetNodeStatus();
    LSEventProcessStatus Resume(LSEventProcessContext context, params string[]? nodes);
    LSEventProcessStatus Fail(LSEventProcessContext context, params string[]? nodes);
    LSEventProcessStatus Cancel(LSEventProcessContext context);

}
