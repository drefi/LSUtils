# LSEventSystem Design Examples

This document contains the complete implementations and practical usage examples for the LSEventSystem architecture described in the main design proposal.

## Core Implementations

### Node Implementations

```csharp
public class SequenceNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    // ExecutionCount removed - tracked in context instead
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    
    public SequenceNode(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        Label = label;
        Priority = priority;
        Condition = condition ?? ((ctx, node) => true);
    }

    public void AddChild(ILSEventNode child) {
        children.Add(child);
        _childrenByLabel[child.Label] = child;
    }
    
    public ILSEventNode? FindChild(string label) {
        return _childrenByLabel.TryGetValue(label, out var child) ? child : null;
    }
    
    public bool HasChild(string label) => _childrenByLabel.ContainsKey(label);
    
    public ILSEventNode[] GetChildren() => children.ToArray();

    public ILSEventNode Clone() {
        var cloned = new SequenceNode(Label, Priority, Condition);
        foreach (var child in children) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) {
            context.TriggerCancelCallbacks();
            return LSEventProcessStatus.FAILURE;
        }
        
        if (Condition(context, this) == false) return LSEventProcessStatus.SUCCESS;
        
        // Record execution in context instead of node state
        context.RecordExecution(Label);
        
        // Sort by priority, then process in sequence
        var sortedChildren = children.OrderBy(c => c.Priority).ToList();
        
        foreach (var child in sortedChildren) {
            if (context.IsCancelled) {
                context.TriggerCancelCallbacks();
                return LSEventProcessStatus.FAILURE;
            }
            
            var childStatus = child.Process(context);
            
            if (childStatus == LSEventProcessStatus.SUCCESS) {
                continue; // Process next child
            } else if (childStatus == LSEventProcessStatus.FAILURE) {
                context.TriggerFailureCallbacks();
                return LSEventProcessStatus.FAILURE;
            } else if (childStatus == LSEventProcessStatus.WAITING) {
                return LSEventProcessStatus.WAITING; // Bubble up waiting
            }
        }
        
        context.TriggerSuccessCallbacks();
        return LSEventProcessStatus.SUCCESS;
    }
}

public class SelectorNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    // ExecutionCount removed - tracked in context instead
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    
    public SelectorNode(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        Label = label;
        Priority = priority;
        Condition = condition ?? ((ctx, node) => true);
    }

    public void AddChild(ILSEventNode child) {
        children.Add(child);
        _childrenByLabel[child.Label] = child;
    }
    
    public ILSEventNode? FindChild(string label) {
        return _childrenByLabel.TryGetValue(label, out var child) ? child : null;
    }
    
    public bool HasChild(string label) => _childrenByLabel.ContainsKey(label);
    
    public ILSEventNode[] GetChildren() => children.ToArray();

    public ILSEventNode Clone() {
        var cloned = new SelectorNode(Label, Priority, Condition);
        foreach (var child in children) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    
    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) {
            context.TriggerCancelCallbacks();
            return LSEventProcessStatus.FAILURE;
        }
        
        if (Condition(context, this) == false) return LSEventProcessStatus.FAILURE;
        
        // Record execution in context instead of node state
        context.RecordExecution(Label);
        
        // Sort by priority, then process until success
        var sortedChildren = children.OrderBy(c => c.Priority).ToList();
        
        foreach (var child in sortedChildren) {
            if (context.IsCancelled) {
                context.TriggerCancelCallbacks();
                return LSEventProcessStatus.FAILURE;
            }
            
            var childStatus = child.Process(context);
            
            if (childStatus == LSEventProcessStatus.SUCCESS) {
                context.TriggerSuccessCallbacks();
                return LSEventProcessStatus.SUCCESS;
            } else if (childStatus == LSEventProcessStatus.WAITING) {
                return LSEventProcessStatus.WAITING; // Bubble up waiting
            }
            // Continue trying other children on FAILURE
        }
        
        context.TriggerFailureCallbacks();
        return LSEventProcessStatus.FAILURE;
    }
}

public class ParallelNode : ILSEventLayerNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    // ExecutionCount removed - tracked in context instead
    protected List<ILSEventNode> children = new();
    protected Dictionary<string, ILSEventNode> _childrenByLabel = new();
    protected int _numRequiredToFail;
    protected int _numRequiredToSucceed;
    private readonly Dictionary<ILSEventNode, LSEventProcessStatus> _childStates = new();

    public ParallelNode(string label, int numRequiredToFail, int numRequiredToSucceed, 
                       LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        Label = label;
        Priority = priority;
        Condition = condition ?? ((ctx, node) => true);
        _numRequiredToFail = numRequiredToFail;
        _numRequiredToSucceed = numRequiredToSucceed;
    }
    
    public void AddChild(ILSEventNode child) {
        children.Add(child);
        _childrenByLabel[child.Label] = child;
    }
    
    public ILSEventNode? FindChild(string label) {
        return _childrenByLabel.TryGetValue(label, out var child) ? child : null;
    }
    
    public bool HasChild(string label) => _childrenByLabel.ContainsKey(label);
    
    public ILSEventNode[] GetChildren() => children.ToArray();

    public ILSEventNode Clone() {
        var cloned = new ParallelNode(Label, _numRequiredToFail, _numRequiredToSucceed, Priority, Condition);
        foreach (var child in children) {
            cloned.AddChild(child.Clone());
        }
        return cloned;
    }
    
    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) {
            context.TriggerCancelCallbacks();
            return LSEventProcessStatus.FAILURE;
        }
        
        if (Condition(context, this) == false) return LSEventProcessStatus.SUCCESS;
        
        // Record execution in context instead of node state
        context.RecordExecution(Label);
        
        var numChildrenSucceeded = 0;
        var numChildrenFailed = 0;
        var hasWaitingChildren = false;
        
        var sortedChildren = children.OrderBy(c => c.Priority).ToList();
        
        foreach (var child in sortedChildren) {
            if (context.IsCancelled) {
                context.TriggerCancelCallbacks();
                return LSEventProcessStatus.FAILURE;
            }
            
            // Check if we already have a state for this child
            if (!_childStates.TryGetValue(child, out var lastStatus)) {
                lastStatus = LSEventProcessStatus.WAITING; // Default state
            }
            
            // Only process children that aren't already completed
            if (lastStatus == LSEventProcessStatus.WAITING) {
                var childStatus = child.Process(context);
                _childStates[child] = childStatus;
                
                switch (childStatus) {
                    case LSEventProcessStatus.SUCCESS:
                        ++numChildrenSucceeded;
                        break;
                    case LSEventProcessStatus.FAILURE:
                        ++numChildrenFailed;
                        break;
                    case LSEventProcessStatus.WAITING:
                        hasWaitingChildren = true;
                        break;
                }
            } else {
                // Use cached result
                switch (lastStatus) {
                    case LSEventProcessStatus.SUCCESS:
                        ++numChildrenSucceeded;
                        break;
                    case LSEventProcessStatus.FAILURE:
                        ++numChildrenFailed;
                        break;
                }
            }
        }
        
        // Check thresholds
        if (_numRequiredToSucceed > 0 && numChildrenSucceeded >= _numRequiredToSucceed) {
            _childStates.Clear(); // Reset for next execution
            context.TriggerSuccessCallbacks();
            return LSEventProcessStatus.SUCCESS;
        }
        
        if (_numRequiredToFail > 0 && numChildrenFailed >= _numRequiredToFail) {
            _childStates.Clear(); // Reset for next execution
            context.TriggerFailureCallbacks();
            return LSEventProcessStatus.FAILURE;
        }
        
        // If we have waiting children, continue waiting
        if (hasWaitingChildren) {
            return LSEventProcessStatus.WAITING;
        }
        
        // No waiting children, but thresholds not met
        _childStates.Clear();
        return LSEventProcessStatus.WAITING;
    }
}

public class HandlerNode : ILSEventNode {
    public string Label { get; }
    public LSPriority Priority { get; }
    public LSEventCondition Condition { get; }
    // ExecutionCount removed - tracked in context instead
    protected LSEventHandlerNode _handler;
    private string? _waitingNodeId;

    public HandlerNode(string label, LSEventHandlerNode handler, 
                      LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        Label = label;
        Priority = priority;
        Condition = condition ?? ((ctx, node) => true);
        _handler = handler;
    }

    public LSEventProcessStatus Process(LSEventProcessContext context) {
        if (context.IsCancelled) return LSEventProcessStatus.FAILURE;
        if (Condition(context, this) == false) return LSEventProcessStatus.SUCCESS;
        
        // Record execution in context instead of node state
        context.RecordExecution(Label);
        
        // Check if this node was already resolved while waiting
        if (_waitingNodeId != null) {
            var resolvedStatus = context.GetResolvedStatus(_waitingNodeId);
            if (resolvedStatus.HasValue) {
                var status = resolvedStatus.Value;
                _waitingNodeId = null; // Clear waiting state
                return status;
            }
        }
        
        // Execute the handler
        var handlerResult = _handler(context, this);
        
        // Handle WAITING status
        if (handlerResult == LSEventProcessStatus.WAITING) {
            // Register this node as waiting if not already registered
            if (_waitingNodeId == null) {
                _waitingNodeId = context.RegisterWaitingNode();
            }
            
            // Check if it was resolved while handler was executing
            var resolvedStatus = context.GetResolvedStatus(_waitingNodeId);
            if (resolvedStatus.HasValue) {
                var status = resolvedStatus.Value;
                _waitingNodeId = null;
                return status;
            }
            
            return LSEventProcessStatus.WAITING;
        }
        
        // Clear waiting state if handler completed normally
        _waitingNodeId = null;
        return handlerResult;
    }

    public ILSEventNode Clone() {
        return new HandlerNode(Label, _handler, Priority, Condition);
    }
}
```

### Context and Builder Implementations

```csharp
public class LSEventProcessContext {
    private readonly Dictionary<ILSEventable, LSEventProcessStatus> _resolvedWaitingNodes = new();
    private readonly List<LSAction<HandlerNode>> _onSuccessCallbacks = new();
    private readonly List<LSAction<HandlerNode>> _onFailureCallbacks = new();
    private readonly List<LSAction<HandlerNode>> _onCancelCallbacks = new();
    private readonly Dictionary<string, int> _executionCounts = new();
    private volatile bool _isCancelled = false;
    private int _waitingNodesCount = 0;
    
    // Event control methods
    public void Resume(int count = 1) {
        for (int i = 0; i < count && _waitingNodesCount > 0; i++) {
            var nodeId = $"waiting_node_{_waitingNodesCount - i}";
            _resolvedWaitingNodes[nodeId] = LSEventProcessStatus.SUCCESS;
        }
    }
    
    public void Fail(int count = 1) {
        for (int i = 0; i < count && _waitingNodesCount > 0; i++) {
            var nodeId = $"waiting_node_{_waitingNodesCount - i}";
            _resolvedWaitingNodes[nodeId] = LSEventProcessStatus.FAILURE;
        }
    }
    
    public void Cancel() {
        _isCancelled = true;
    }
    
    // Execution tracking methods
    internal void RecordExecution(string nodeLabel) {
        if (_executionCounts.ContainsKey(nodeLabel)) {
            _executionCounts[nodeLabel]++;
        } else {
            _executionCounts[nodeLabel] = 1;
        }
    }
    
    public int GetExecutionCount(string nodeLabel) {
        return _executionCounts.TryGetValue(nodeLabel, out var count) ? count : 0;
    }
    
    // Internal methods for nodes
    internal string RegisterWaitingNode() {
        var nodeId = $"waiting_node_{++_waitingNodesCount}";
        return nodeId;
    }
    
    internal LSEventProcessStatus? GetResolvedStatus(string nodeId) {
        return _resolvedWaitingNodes.TryGetValue(nodeId, out var status) ? status : null;
    }
    
    internal bool IsCancelled => _isCancelled;
    
    // Callback system
    public LSEventProcessContext OnSuccess(List<LSAction<HandlerNode>> callback) {
        _onSuccessCallbacks.Add(callback);
        return this;
    }
    
    public LSEventProcessContext OnFailure(List<LSAction<HandlerNode>> callback) {
        _onFailureCallbacks.Add(callback);
        return this;
    }
    
    public LSEventProcessContext OnCancel(List<LSAction<HandlerNode>> callback) {
        _onCancelCallbacks.Add(callback);
        return this;
    }
    
    internal void TriggerSuccessCallbacks() => _onSuccessCallbacks.ForEach(cb => cb());
    internal void TriggerFailureCallbacks() => _onFailureCallbacks.ForEach(cb => cb());
    internal void TriggerCancelCallbacks() => _onCancelCallbacks.ForEach(cb => cb());
}

public class LSEventContextBuilder {
    // Core building
    private Stack<ILSEventLayerNode> _layerNodeStack = new();
    private ILSEventNode? _rootNode;
    
    // Injection mode
    private readonly ILSEventNode? _baseContext;
    private readonly List<InjectionPoint> _injections = new();
    private ILSEventNode? _compiledContext;
    
    private record InjectionPoint(string ParentPath, ILSEventNode NodeToInject);
    
    // Constructor for creation mode
    public LSEventContextBuilder() {
        _baseContext = null;
    }
    
    // Constructor for injection mode
    internal LSEventContextBuilder(ILSEventNode baseContext) {
        _baseContext = baseContext;
    }
    
    private bool IsInjectionMode => _baseContext != null;

    public LSEventContextBuilder Sequence(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        if (IsInjectionMode) {
            return SequenceInjection(label, priority, condition);
        } else {
            var sequenceNode = new SequenceNode(label, priority, condition);
            return PushLayer(sequenceNode);
        }
    }
    
    public LSEventContextBuilder Selector(string label, LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        if (IsInjectionMode) {
            return SelectorInjection(label, priority, condition);
        } else {
            var selectorNode = new SelectorNode(label, priority, condition);
            return PushLayer(selectorNode);
        }
    }
    
    public LSEventContextBuilder Parallel(string label, int numRequiredToFail, int numRequiredToSucceed, 
                                        LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        if (IsInjectionMode) {
            return ParallelInjection(label, numRequiredToFail, numRequiredToSucceed, priority, condition);
        } else {
            var parallelNode = new ParallelNode(label, numRequiredToFail, numRequiredToSucceed, priority, condition);
            return PushLayer(parallelNode);
        }
    }
    
    public LSEventContextBuilder Do(string label, LSEventHandlerNode handler, 
                                  LSPriority priority = LSPriority.Normal, LSEventCondition? condition = null) {
        if (IsInjectionMode) {
            return DoInjection(label, handler, priority, condition);
        } else {
            if (_layerNodeStack.Count <= 0) {
                throw new LSException("Can't create unnested HandlerNode, it must be a leaf node.");
            }
            
            var handlerNode = new HandlerNode(label, handler, priority, condition);
            _layerNodeStack.Peek().AddChild(handlerNode);
            return this;
        }
    }
    
    public LSEventContextBuilder When(string label, LSEventCondition condition, LSEventHandlerNode handler, 
                                    LSPriority priority = LSPriority.Normal) {
        return Do(label, handler, priority, condition);
    }
    
    public LSEventContextBuilder Splice(ILSEventLayerNode subLayer) {
        if (_layerNodeStack.Count <= 0) {
            throw new LSException("Can't splice an unnested sub-tree, there must be a parent-tree.");
        }

        _layerNodeStack.Peek().AddChild(subLayer);
        return this;
    }

    public LSEventContextBuilder End() {
        if (IsInjectionMode) {
            return EndInjection();
        } else {
            if (_layerNodeStack.Count == 0) {
                throw new LSException("No layer to end.");
            }
            
            var currentLayer = _layerNodeStack.Pop();
            
            if (_layerNodeStack.Count == 0) {
                _rootNode = currentLayer; // This becomes our root
            }
            
            return this;
        }
    }

    public ILSEventNode Build() {
        if (IsInjectionMode) {
            return Compile();
        } else {
            if (_rootNode == null) {
                throw new LSException("No context built. Make sure to call End() on the root layer.");
            }
            return _rootNode;
        }
    }
    
    // Creation mode helpers
    private LSEventContextBuilder PushLayer(ILSEventLayerNode layer) {
        if (_layerNodeStack.Count > 0) {
            _layerNodeStack.Peek().AddChild(layer);
        }
        
        _layerNodeStack.Push(layer);
        return this;
    }
    
    // Injection mode implementations
    private LSEventContextBuilder SequenceInjection(string label, LSPriority priority, LSEventCondition? condition) {
        var existingNode = FindNodeInContext(label);
        
        if (existingNode is SequenceNode existingSequence) {
            _layerNodeStack.Push(existingSequence);
        } else if (existingNode != null) {
            throw new LSException($"Node '{label}' exists but is not a SequenceNode.");
        } else {
            var newSequence = new SequenceNode(label, priority, condition);
            QueueForInjection(newSequence);
            _layerNodeStack.Push(newSequence);
        }
        
        return this;
    }
    
    private LSEventContextBuilder SelectorInjection(string label, LSPriority priority, LSEventCondition? condition) {
        var existingNode = FindNodeInContext(label);
        
        if (existingNode is SelectorNode existingSelector) {
            _layerNodeStack.Push(existingSelector);
        } else if (existingNode != null) {
            throw new LSException($"Node '{label}' exists but is not a SelectorNode.");
        } else {
            var newSelector = new SelectorNode(label, priority, condition);
            QueueForInjection(newSelector);
            _layerNodeStack.Push(newSelector);
        }
        
        return this;
    }
    
    private LSEventContextBuilder ParallelInjection(string label, int numRequiredToFail, int numRequiredToSucceed,
                                                   LSPriority priority, LSEventCondition? condition) {
        var existingNode = FindNodeInContext(label);
        
        if (existingNode is ParallelNode existingParallel) {
            _layerNodeStack.Push(existingParallel);
        } else if (existingNode != null) {
            throw new LSException($"Node '{label}' exists but is not a ParallelNode.");
        } else {
            var newParallel = new ParallelNode(label, numRequiredToFail, numRequiredToSucceed, priority, condition);
            QueueForInjection(newParallel);
            _layerNodeStack.Push(newParallel);
        }
        
        return this;
    }
    
    private LSEventContextBuilder DoInjection(string label, LSEventHandlerNode handler, 
                                            LSPriority priority, LSEventCondition? condition) {
        if (_layerNodeStack.Count == 0) {
            throw new LSException("Can't inject handler without a parent layer.");
        }
        
        var handlerNode = new HandlerNode(label, handler, priority, condition);
        _layerNodeStack.Peek().AddChild(handlerNode);
        return this;
    }
    
    private LSEventContextBuilder EndInjection() {
        if (_layerNodeStack.Count == 0) {
            throw new LSException("No injection layer to end.");
        }
        
        _layerNodeStack.Pop();
        return this;
    }
    
    private ILSEventNode Compile() {
        if (_compiledContext != null) return _compiledContext;
        
        // Clone the base context
        _compiledContext = _baseContext!.Clone();
        
        // Apply all queued injections
        foreach (var injection in _injections) {
            ApplyInjection(_compiledContext, injection);
        }
        
        return _compiledContext;
    }
    
    private ILSEventNode? FindNodeInContext(string label) {
        return FindNodeRecursive(_baseContext!, label);
    }
    
    private ILSEventNode? FindNodeRecursive(ILSEventNode node, string targetLabel) {
        if (node.Label == targetLabel) return node;
        
        if (node is ILSEventLayerNode layerNode) {
            var found = layerNode.FindChild(targetLabel);
            if (found != null) return found;
            
            // Deep search in children
            foreach (var child in layerNode.GetChildren()) {
                var deepFound = FindNodeRecursive(child, targetLabel);
                if (deepFound != null) return deepFound;
            }
        }
        
        return null;
    }
    
    private void QueueForInjection(ILSEventNode node) {
        var parentPath = GetCurrentPath();
        _injections.Add(new InjectionPoint(parentPath, node));
    }
    
    private string GetCurrentPath() {
        if (_layerNodeStack.Count == 0) return "";
        return _layerNodeStack.Peek().Label;
    }
    
    private void ApplyInjection(ILSEventNode context, InjectionPoint injection) {
        ILSEventLayerNode targetParent;
        
        if (string.IsNullOrEmpty(injection.ParentPath)) {
            // Root level injection - context itself must be a layer node
            if (context is not ILSEventLayerNode rootLayer) {
                throw new LSException("Cannot inject at root level - context is not a layer node.");
            }
            targetParent = rootLayer;
        } else {
            // Find the parent node
            var parentNode = FindNodeRecursive(context, injection.ParentPath);
            
            if (parentNode == null) {
                throw new LSException($"Parent node '{injection.ParentPath}' not found for injection.");
            }
            
            if (parentNode is not ILSEventLayerNode layerParent) {
                throw new LSException($"Parent node '{injection.ParentPath}' is not a layer node and cannot accept children.");
            }
            
            targetParent = layerParent;
        }
        
        // Check for conflicts
        if (targetParent.HasChild(injection.NodeToInject.Label)) {
            throw new LSException($"Node with label '{injection.NodeToInject.Label}' already exists in parent '{injection.ParentPath}'.");
        }
        
        // Perform the injection
        targetParent.AddChild(injection.NodeToInject);
    }
}
```

### Context Management System

```csharp
public class LSEventManager {
    private static readonly Dictionary<ILSEventable, ILSEventNode> _globalContexts = new();
    
    public void RegisterContext(ILSEventable instance, ILSEventNode context) {
        _globalContexts[instance] = context;
    }
    
    public static ILSEventNode? GetContext(ILSEventable instance) {
        return _globalContexts.TryGetValue(instance, out var context) ? context : null;
    }
    
    public static LSEventContextBuilder CreateBuilder(string? contextName = null) {
        if (contextName == null) {
            // Creation mode
            return new LSEventContextBuilder();
        } else {
            // Injection mode
            var baseContext = GetContext(contextName);
            if (baseContext == null) {
                throw new LSException($"Global context '{contextName}' not found.");
            }
            
            return new LSEventContextBuilder(baseContext);
        }
    }
}

```

## Usage Examples

### Global Context Setup

```csharp
// Create and register global context using creation mode
var globalContext = LSEventContextManager.CreateBuilder() // Creation mode (no context name)
    .Sequence("root")
        .Sequence("validate")
            .When("A", conditionA, handlerA)
            .When("B", conditionB, handlerB)
            .When("C", conditionC, handlerC)
        .End()
        .Selector("configure")
            .Do("D", handlerD)
            .Do("E", handlerE)
        .End()
        .Parallel("execute", 1, 3)
            .Do("F", handlerF)
            .Do("G", handlerG)
        .End()
    .End()
    .Build();

LSEventContextManager.RegisterContext("default", globalContext);
```

### Event-Specific Context with Injection

```csharp
// Create event context with runtime injections
var @event = new MyEvent();
@event.Process("default", (injectionBuilder) => {
    injectionBuilder
        .Sequence("root") // Checks that Sequence with label "root" exists
            .Selector("cleanup") // "cleanup" doesn't exist, creates new
                .Do("H", handlerH)
            .End()
        .End();
});

// Alternative usage with unified builder
var injectionBuilder = LSEventContextManager.CreateBuilder("default"); // Injection mode
var runtimeContext = injectionBuilder
    .Sequence("root")
        .Parallel("monitoring", 0, 1) // Add monitoring in parallel
            .Do("LogPerformance", performanceLogger)
            .Do("TrackMetrics", metricsTracker)
        .End()
    .End()
    .Build(); // Build() calls Compile() in injection mode

// Process with the compiled context
var result = runtimeContext.Process(eventContext);
```

## Event Processing API

### Event Processing with Waiting Logic

```csharp
// Event class with waiting state management
public class MyEvent {
    private LSEventProcessContext? _currentContext;
    
    public LSEventProcessStatus Process(string contextName, Action<LSEventContextBuilder>? injectionConfig = null) {
        var baseContext = LSEventContextManager.GetContext(contextName);
        if (baseContext == null) {
            throw new LSException($"Context '{contextName}' not found.");
        }
        
        ILSEventNode runtimeContext = baseContext;
        
        // Apply injections if provided
        if (injectionConfig != null) {
            var injectionBuilder = LSEventContextManager.CreateBuilder(contextName); // Injection mode
            injectionConfig(injectionBuilder);
            runtimeContext = injectionBuilder.Build(); // Build() calls Compile() in injection mode
        }
        
        _currentContext = new LSEventProcessContext()
            .OnSuccess(() => Console.WriteLine("Event completed successfully"))
            .OnFailure(() => Console.WriteLine("Event failed"))
            .OnCancel(() => Console.WriteLine("Event was cancelled"));
        
        return runtimeContext.Process(_currentContext);
    }
    
    // Event control methods
    public void Resume(int count = 1) {
        _currentContext?.Resume(count);
    }
    
    public void Fail(int count = 1) {
        _currentContext?.Fail(count);
    }
    
    public void Cancel() {
        _currentContext?.Cancel();
    }
}

// Usage examples with waiting handlers
LSEventHandlerNode asyncHandler = (context, node) => {
    // Start async operation
    SomeAsyncOperation.Start();
    return LSEventProcessStatus.WAITING;
};

LSEventHandlerNode regularHandler = (context, node) => {
    // Regular synchronous operation
    DoSomeWork();
    return LSEventProcessStatus.SUCCESS;
};

// Set up context with waiting handlers
var globalContext = LSEventContextManager.CreateBuilder() // Creation mode
    .Sequence("root")
        .Do("AsyncTask1", asyncHandler)
        .Do("AsyncTask2", asyncHandler)
        .Parallel("ConcurrentTasks", 1, 2)
            .Do("Task3", asyncHandler)
            .Do("Task4", regularHandler)
        .End()
    .End()
    .Build();

LSEventContextManager.RegisterContext("default", globalContext);

// Process event with external control
var myEvent = new MyEvent();
var result = myEvent.Process("default"); // Returns WAITING

// Event receives external signals and controls execution
myEvent.Resume(2); // Resume 2 waiting tasks
myEvent.Fail(1);   // Fail 1 waiting task

// Or cancel everything immediately
myEvent.Cancel();
```

## Advanced Usage Patterns

### Complex Condition Handling

```csharp
// Define conditions with context access
LSEventCondition dynamicCondition = (context, node) => {
    var executionCount = context.GetExecutionCount(node.Label);
    return executionCount < 3; // Only execute first 3 times
};

LSEventCondition contextualCondition = (context, node) => {
    return context.GetData<bool>("shouldExecute", false);
};

// Use in context building
var contextWithConditions = LSEventContextManager.CreateBuilder() // Creation mode
    .Sequence("conditional_flow")
        .When("RetryableTask", dynamicCondition, retryHandler)
        .When("ConditionalTask", contextualCondition, conditionalHandler)
    .End()
    .Build();
```

### Error Handling and Recovery

```csharp
// Selector pattern for fallback behavior
var resilientContext = LSEventContextManager.CreateBuilder() // Creation mode
    .Sequence("resilient_processing")
        .Selector("error_handling")
            .Do("PrimaryMethod", primaryHandler)
            .Do("FallbackMethod", fallbackHandler)
            .Do("EmergencyMethod", emergencyHandler)
        .End()
        .Do("FinalCleanup", cleanupHandler)
    .End()
    .Build();

// Handler with error recovery
LSEventHandlerNode resilientHandler = (context, node) => {
    try {
        // Attempt primary operation
        var result = PerformOperation();
        return result ? LSEventProcessStatus.SUCCESS : LSEventProcessStatus.FAILURE;
    }
    catch (Exception ex) {
        // Log error and signal failure for fallback
        Logger.Error($"Handler {node.Label} failed: {ex.Message}");
        return LSEventProcessStatus.FAILURE;
    }
};
```

### Performance Monitoring and Metrics

```csharp
// Context with integrated monitoring
var monitoredContext = new LSEventContextBuilder()
    .Sequence("monitored_flow")
        .Parallel("main_and_monitoring", 1, 2)
            .Sequence("main_operations")
                .Do("BusinessLogic1", businessHandler1)
                .Do("BusinessLogic2", businessHandler2)
            .End()
            .Sequence("monitoring")
                .Do("CollectMetrics", metricsHandler)
                .Do("UpdateDashboard", dashboardHandler)
            .End()
        .End()
    .End()
    .Build();

// Metrics collection handler
LSEventHandlerNode metricsHandler = (context, node) => {
    var totalExecutions = context.GetExecutionCount("BusinessLogic1") + 
                         context.GetExecutionCount("BusinessLogic2");
    
    MetricsCollector.Record("total_business_operations", totalExecutions);
    return LSEventProcessStatus.SUCCESS;
};
```

### Runtime Context Modification

```csharp
// Dynamic context modification based on event data
public LSEventProcessStatus ProcessWithDynamicInjection(MyEvent eventData, string baseContextName) {
    var injectionBuilder = LSEventContextManager.CreateInjectionBuilder(baseContextName);
    
    // Add handlers based on event properties
    if (eventData.RequiresValidation) {
        injectionBuilder
            .Sequence("root")
                .Sequence("validation")
                    .Do("ValidateInput", inputValidator)
                    .Do("ValidatePermissions", permissionValidator)
                .End()
            .End();
    }
    
    if (eventData.RequiresAudit) {
        injectionBuilder
            .Sequence("root")
                .Do("LogAuditEvent", auditLogger)
            .End();
    }
    
    // Add environment-specific handlers
    if (Environment.IsDevelopment()) {
        injectionBuilder
            .Sequence("root")
                .Do("DebugOutput", debugHandler)
            .End();
    }
    
    var runtimeContext = injectionBuilder.Compile();
    var processContext = new LSEventProcessContext();
    
    return runtimeContext.Process(processContext);
}
```

### Integration with Existing LSEvent System

```csharp
// Bridge between old and new systems
public class LSEventSystemBridge {
    public static void MigrateEvent<T>(T eventData) where T : LSEvent {
        // Get existing event type mapping
        var eventType = typeof(T).Name;
        var contextName = GetContextNameForEventType(eventType);
        
        // Create processing context
        var context = new LSEventProcessContext()
            .OnSuccess(() => {
                // Trigger existing success handlers
                LSDispatcher.TriggerSuccess(eventData);
            })
            .OnFailure(() => {
                // Trigger existing failure handlers  
                LSDispatcher.TriggerFailure(eventData);
            });
        
        // Process using new system
        var baseContext = LSEventContextManager.GetContext(contextName);
        var result = baseContext.Process(context);
        
        // Handle waiting state
        if (result == LSEventProcessStatus.WAITING) {
            // Register for external completion
            RegisterForCompletion(eventData, context);
        }
    }
    
    private static void RegisterForCompletion<T>(T eventData, LSEventProcessContext context) where T : LSEvent {
        // Hook into existing completion mechanisms
        eventData.OnComplete += (success) => {
            if (success) {
                context.Resume();
            } else {
                context.Fail();
            }
        };
    }
}
```
