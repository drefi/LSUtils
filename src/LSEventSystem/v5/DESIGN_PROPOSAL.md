# LSEventSystem

## Handler Execution Behaviour

### LSEvent delegates

This should be the basic handler signature.

```csharp
public delegate LSEventProcessStatus LSEventHandlerNode(LSEventProcessContext context, ILSEventNode node);
public delegate bool LSEventCondition(LSEventProcessContext context, ILSEventNode node);
```

### Process Status

```csharp
public enum LSEventProcessStatus {
    SUCCESS,
    FAILURE,
    WAITING,
}
```

### Nodes

- Nodes will replace the current Phase and handler based classification;
- Should be possible to nest nodes (for complex behaviour);
- Priority is applied to nodes in the same level of execution;

### Nodes Behaviour

- SEQUENCE: Process nodes until LSEventProcessStatus is different of **SUCCESS**, if all nodes are processed LSEventProcessStatus is **SUCCESS**;
- SELECTOR: Process nodes until LSEventProcessStatus is different of **FAILURE**, if all nodes are processed LSEventProcessStatus is **FAILURE**;
- PARALLEL: Process nodes in sequence, LSEventProcessStatus is based in the rules set in the node construction;
- HANDLER: Process a LSEventHandler;
- INVERTER: Invert the result of the next processed node;

## Node interface

```csharp
public interface ILSEventNode {
    string Label { get; }  // Used as path identifier for navigation
    LSPriority Priority { get; }
    LSEventCondition Condition { get; }
    // ExecutionCount removed - now tracked in LSEventProcessContext for cloning compatibility
    LSEventProcessStatus Process(LSEventProcessContext context);
    ILSEventNode Clone();
}

public interface ILSEventLayerNode : ILSEventNode {
    void AddChild(ILSEventNode child);
    ILSEventNode? FindChild(string label);
    bool HasChild(string label);
    ILSEventNode[] GetChildren(); // Exposes children for navigation and cloning
}
```

## Node Implementations

```csharp
public class SequenceNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    
    public SequenceNode(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public void AddChild(ILSEventNode child);
    public ILSEventNode? FindChild(string label);
    public bool HasChild(string label);
    public ILSEventNode[] GetChildren();
    public ILSEventNode Clone();
    public LSEventProcessStatus Process(LSEventProcessContext context);
}

public class SelectorNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    
    public SelectorNode(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public void AddChild(ILSEventNode child);
    public ILSEventNode? FindChild(string label);
    public bool HasChild(string label);
    public ILSEventNode[] GetChildren();
    public ILSEventNode Clone();
    public LSEventProcessStatus Process(LSEventProcessContext context);
}

public class ParallelNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    protected int _numRequiredToFail;
    protected int _numRequiredToSucceed;
    private readonly Dictionary<ILSEventNode, LSEventProcessStatus> _childStates = new();

    public ParallelNode(string label, int numRequiredToFail, int numRequiredToSucceed, 
                       LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public void AddChild(ILSEventNode child);
    public ILSEventNode? FindChild(string label);
    public bool HasChild(string label);
    public ILSEventNode[] GetChildren();
    public ILSEventNode Clone();
    public LSEventProcessStatus Process(LSEventProcessContext context);
}

public class HandlerNode : ILSEventNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    protected LSEventHandlerNode _handler;
    private string? _waitingNodeId;

    public HandlerNode(string label, LSEventHandlerNode handler, 
                      LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public LSEventProcessStatus Process(LSEventProcessContext context);
    public ILSEventNode Clone();
}
```

## Context and Builder Classes

```csharp
public class LSEventProcessContext {
    private readonly Dictionary<string, LSEventProcessStatus> _resolvedWaitingNodes = new();
    private readonly List<Action> _onSuccessCallbacks = new();
    private readonly List<Action> _onFailureCallbacks = new();
    private readonly List<Action> _onCancelCallbacks = new();
    private readonly Dictionary<string, int> _executionCounts = new();
    private volatile bool _isCancelled = false;
    private int _waitingNodesCount = 0;
    
    // Event control methods
    public void Resume(int count = 1);
    public void Fail(int count = 1);
    public void Cancel();
    
    // Execution tracking methods
    internal void RecordExecution(string nodeLabel);
    public int GetExecutionCount(string nodeLabel);
    
    // Internal methods for nodes
    internal string RegisterWaitingNode();
    internal LSEventProcessStatus? GetResolvedStatus(string nodeId);
    internal bool IsCancelled { get; }
    
    // Callback system
    public LSEventProcessContext OnSuccess(Action callback);
    public LSEventProcessContext OnFailure(Action callback);
    public LSEventProcessContext OnCancel(Action callback);
    
    internal void TriggerSuccessCallbacks();
    internal void TriggerFailureCallbacks();
    internal void TriggerCancelCallbacks();
}

public class LSEventContextBuilder {
    private readonly ILSEventNode? _baseContext; // null = build mode, not null = injection mode
    private Stack<ILSEventLayerNode> _layerNodeStack = new();
    private ILSEventNode? _rootNode;
    private readonly List<InjectionPoint> _injections = new();
    
    private record InjectionPoint(string ParentPath, ILSEventNode NodeToInject);

    // Constructor for building from scratch
    public LSEventContextBuilder();
    
    // Constructor for injection mode  
    public LSEventContextBuilder(ILSEventNode baseContext);

    public LSEventContextBuilder Sequence(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public LSEventContextBuilder Selector(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public LSEventContextBuilder Parallel(string label, int numRequiredToFail, int numRequiredToSucceed, 
                                        LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public LSEventContextBuilder Do(string label, LSEventHandlerNode handler, 
                                  LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null);
    public LSEventContextBuilder When(string label, LSEventCondition condition, LSEventHandlerNode handler, 
                                    LSPriority priority = LSPriority.Normal);
    public LSEventContextBuilder Splice(ILSEventLayerNode subLayer);
    public LSEventContextBuilder End();
    public ILSEventNode Build(); // Handles both build and injection compilation
    
    // Private methods for both modes
    private LSEventContextBuilder PushLayer(ILSEventLayerNode layer);
    private ILSEventNode? FindNodeInContext(string label);
    private ILSEventNode? FindNodeRecursive(ILSEventNode node, string targetLabel);
    private void QueueForInjection(ILSEventNode node);
    private string GetCurrentPath();
    private ILSEventNode CloneContext(ILSEventNode node);
    private void ApplyInjection(ILSEventNode context, InjectionPoint injection);
}
```

## Context Management Classes

```csharp
public static class LSEventContextManager {
    private static readonly Dictionary<string, ILSEventNode> _globalContexts = new();
    
    public static void RegisterContext(string name, ILSEventNode context);
    public static ILSEventNode? GetContext(string name);
    public static LSEventContextBuilder CreateBuilder(string? contextName = null);
}
```

## Implementation Details

For complete method implementations with full logic and behavior, see [DESIGN_PROPOSAL_EXAMPLES.md](./DESIGN_PROPOSAL_EXAMPLES.md).

The implementations include:

- **SequenceNode**: Processes children in order until failure
- **SelectorNode**: Processes children until first success  
- **ParallelNode**: Processes children concurrently with thresholds
- **HandlerNode**: Executes handler functions with waiting logic
- **LSEventContextBuilder**: Unified fluent API for building and injecting node trees
- **LSEventProcessContext**: Context for execution tracking and control
- **LSEventContextManager**: Global context registration and builder creation

## Context Management System

The context management system provides global context registration and runtime injection capabilities. See [DESIGN_PROPOSAL_EXAMPLES.md](./DESIGN_PROPOSAL_EXAMPLES.md) for complete implementation details.

### Key Features

- **Global Context Registration**: Register and retrieve node trees by name
- **Runtime Injection**: Modify existing contexts with new nodes at runtime  
- **Clone-Safe Operations**: All contexts support safe cloning for concurrent use
- **Execution Tracking**: Context-based execution counting for stateless nodes

## Usage Examples

For comprehensive usage examples and practical implementations, see [DESIGN_PROPOSAL_EXAMPLES.md](./DESIGN_PROPOSAL_EXAMPLES.md).

The examples file includes:

- Global context setup patterns
- Event-specific context injection
- Event processing with waiting logic
- Advanced usage patterns with conditions
- Error handling and recovery strategies
- Performance monitoring and metrics
- Runtime context modification
- Integration with existing LSEvent system
