# LSProcess Pipeline Instructions - CORRECTED

## Core Concept

LSProcess is a tree-based processing framework where:

- **Process** = Data container with execution lifecycle
- **LSProcessSession** = Runtime execution state management
- **Node Hierarchy** = Processing logic and flow control

## Execution Pipeline (CRITICAL)

**Context Merge Order (lowest to highest priority):**

```context
1. Global context (registered without instance) 
2. Instance-specific context (registered for specific ILSProcessable)
3. processing() context (override method - ALWAYS RUNS)
4. WithProcessing() context (runtime configuration - HIGHEST PRIORITY)
```

**Node Override vs Merge Rules:**

- **Handlers** with the same ID override (replace) lower-priority ones
- **Decorators** (Sequence, Selector, Parallel, Inverter) with the same ID MERGE (children combined across contexts)
- ReadOnly nodes cannot be overridden or merged
- WithProcessing() overrides/merges on top of processing()
- processing() always executes, even when WithProcessing() is provided

## Single Execution Model

```csharp
var process = new MyProcess();
var result = process.Execute();  // First call - executes workflow
var result2 = process.Execute(); // Subsequent calls return cached result
```

## Processing Configuration Pattern

```csharp
// Method 1: Define defaults in processing()
public class MyProcess : LSProcess {
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
        // Add default handlers - these always run but can be overridden
        return builder
            .Sequence("workflow", seq => seq
                .Handler("default-step", DefaultHandler)
            );
    }
}

// Method 2: Override at runtime with WithProcessing()
var process = new MyProcess()
    .WithProcessing(builder => builder
        .Sequence("workflow", seq => seq  // Same ID - decorator MERGES children
            .Handler("custom-step", CustomHandler)  // Handler overrides matching ID
            .Handler("extra-step", ExtraHandler)    // New handler appended
        )
    );

// Method 3: Register context at manager level
// Global (all instances)
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Sequence("workflow", seq => seq
        .Handler("global-handler", GlobalHandler)
    )
);

// Instance-specific
var entity = new Entity();
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Sequence("workflow", seq => seq
        .Handler("entity-handler", EntityHandler)
    )
, entity);
```

## Data Exchange

```csharp
// Handlers communicate through process data store
LSProcessHandler handler1 = session => {
    session.Process.SetData("key", value);
    return LSProcessResultStatus.SUCCESS;
};

LSProcessHandler handler2 = session => {
    var value = session.Process.GetData<T>("key");
    if (session.Process.TryGetData<T>("optional-key", out var optional)) {
        // Use optional data
    }
    return LSProcessResultStatus.SUCCESS;
};
```

## Asynchronous Operations

```csharp
LSProcessHandler asyncHandler = session => {
    StartAsyncOperation();
    return LSProcessResultStatus.WAITING; // Process will wait
};

var result = process.Execute();  // Returns WAITING
// ... external event occurs ...
result = process.Resume("node-id");  // Continue execution
result = process.Fail("node-id");    // Force failure
process.Cancel();                    // Force cancellation (terminal)
```

## Key Status Codes

- **SUCCESS** - Operation completed successfully
- **FAILURE** - Operation failed
- **WAITING** - Waiting for external event (can Resume/Fail)
- **CANCELLED** - Cancelled by user (terminal, cannot resume)
- **UNKNOWN** - Initial state

## Node Types

| Type | Logic | Merge/Override Behavior | When to Use |
| ---- | ----- | ---------------------- | --------- |
| **Sequence** | AND (all must succeed) | Decorator: MERGES children | Validation chains, step-by-step workflows |
| **Selector** | OR (first success wins) | Decorator: MERGES children | Fallback strategies, alternative paths |
| **Parallel** | Concurrent with thresholds | Decorator: MERGES children | Concurrent ops, majority voting |
| **Inverter** | NOT (inverts SUCCESS/FAILURE) | Decorator: MERGES child | Negative conditions, rejection logic |
| **Handler** | Terminal execution | Overrides when IDs match | Actual business logic |

## Test Case: NodeOverridePriority_Pipeline_MergeAndOverride_WorksCorrectly

Source: [tests/ProcessSystem/LSProcessTests.cs#L603-L667](tests/ProcessSystem/LSProcessTests.cs#L603-L667)

What it verifies:

- Decorators merge: `checkContextMerge` Sequence collects children from Global → Instance → processing() → WithProcessing()
  - Expected log: Global, Instanced, processing, WithProcessing
- Handlers override: `override` Sequence keeps only the highest-priority handler (WithProcessing)
  - Expected log tail: WithProcessingOverride

Pipeline confirmed: Global → Instance → processing() → WithProcessing()
Merge/override confirmed: Decorators merge, Handlers override

## Best Practices

1. **Use processing() for defaults** - Define behavior that should always run
2. **Use WithProcessing() for overrides** - Customize behavior at runtime
3. **Use Global contexts for shared behavior** - Register once, reuse everywhere
4. **Use Instance contexts for entity-specific logic** - Customize per entity/user
5. **Mark immutable nodes as readonly** - Prevent critical overrides
6. **Return status codes, not exceptions** - Use FAILURE instead of throwing
7. **Use meaningful node IDs** - Required for Resume/Fail operations
8. **Avoid side effects in handlers** - Keep handlers pure, use SetData for state
9. **Remember merge vs override** - Decorators merge children; Handlers override by ID

## Common Mistakes to Avoid

❌ **Wrong**: Trying to override processing() with WithProcessing() when readonly
❌ **Wrong**: Assuming WithProcessing() prevents processing() from running
❌ **Wrong**: Calling Execute() multiple times expecting different results (cached)
❌ **Wrong**: Not checking IsCancelled before resuming WAITING nodes
✅ **Right**: Use node IDs consistently across contexts
✅ **Right**: Override only the nodes you need to customize
✅ **Right**: Let processing() provide defaults, WithProcessing() for overrides
