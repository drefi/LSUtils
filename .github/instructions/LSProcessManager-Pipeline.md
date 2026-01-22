# LSProcessManager Context Registration Guide

## Overview

LSProcessManager is the central registry for process contexts. It manages a two-level hierarchy:
- **Process Type** → **Instance** → **Node Hierarchy**

This enables flexible, multi-layered behavior definition without modifying code or core logic.

## Core Responsibilities

1. **Context Storage**: Maintain registered node hierarchies per process type and instance
2. **Context Merging**: Combine contexts from global, instance, and local layers
3. **Instance Behavior**: Support multiple instance targeting strategies (match, multi, all)
4. **Thread Safety**: Concurrent-safe registration and retrieval via ConcurrentDictionary

## Registration Levels (Priority Order)

```
Highest Priority (Runtime)
        ↓
Local context (WithProcessing) - Process-specific override
        ↓
Instance context - Entity/user-specific behavior
        ↓
Global context - Applies to all instances
        ↓
Lowest Priority (Built-in defaults)
```

## Registration Patterns

### Global Context

Applied to all process instances of a given type:

```csharp
// Register once, used everywhere
LSProcessManager.Singleton.Register<AttributeRecomputeProcess>(root => root
    .Sequence("base-recompute", seq => seq
        .Handler("seed-base", SeedBase)
        .Handler("apply-modifiers", ApplyModifiers)
    )
);
```

**Use cases:**
- Common behavior shared across all entities
- Default modifier application logic
- Standard validation/audit steps

### Instance Context

Applied only to specific ILSProcessable instances:

```csharp
var vipEntity = new VIPEntity { ID = Guid.Parse("...") };

LSProcessManager.Singleton.Register<AttributeRecomputeProcess>(root => root
    .Sequence("base-recompute", seq => seq
        .Handler("vip-bonus", ApplyVIPBonus)      // Override global logic
        .Handler("parallel-cascade", cascade => cascade  // Override cascade strategy
            .Handler("update-dep-1", UpdateDep1)
            .Handler("update-dep-2", UpdateDep2)
        )
    )
, instance: vipEntity);
```

**Use cases:**
- Per-entity customization (VIP, faction, class)
- Entity-specific modifiers or bonuses
- Different cascade/dependency strategies per entity

### Local Context (Runtime)

Applied via `WithProcessing()` on a specific process instance:

```csharp
var process = new AttributeRecomputeProcess(entity, definer, world);
process.WithProcessing(b => b
    .Handler("temporary-buff", session => {
        var current = session.Process.GetData<float>("current");
        session.Process.SetData("current", current * 1.5f);
        return LSProcessResultStatus.SUCCESS;
    })
);

var result = process.Execute(manager, ProcessInstanceBehaviour.ALL, entity);
```

**Use cases:**
- One-time customizations (buffs, debuffs)
- Test-specific behavior overrides
- Dynamic effect injection without touching definers

## Context Merging Behavior

### Decorators: MERGE

When decorator IDs match, children are **combined** across layers:

```csharp
// Global: base sequence with 2 handlers
manager.Register<MyProcess>(root => root
    .Sequence("cascade", seq => seq
        .Handler("step-1", Handler1)
        .Handler("step-2", Handler2)
    )
);

// Instance: same ID "cascade", adds step-3
manager.Register<MyProcess>(root => root
    .Sequence("cascade", seq => seq
        .Handler("step-3", Handler3)
    )
, entity);

// Local: same ID "cascade", adds step-4
process.WithProcessing(b => b
    .Sequence("cascade", seq => seq
        .Handler("step-4", Handler4)
    )
);

// Result: Sequence "cascade" contains [step-1, step-2, step-3, step-4]
```

### Handlers: OVERRIDE

When handler IDs match, **highest-priority wins**:

```csharp
// Global: mod-bonus +5
manager.Register<MyProcess>(root => root
    .Handler("mod-bonus", session => {
        // add 5
        return LSProcessResultStatus.SUCCESS;
    })
);

// Instance: mod-bonus +10 (overrides global)
manager.Register<MyProcess>(root => root
    .Handler("mod-bonus", session => {
        // add 10 instead
        return LSProcessResultStatus.SUCCESS;
    })
, entity);

// Local: mod-bonus +15 (overrides instance and global)
process.WithProcessing(b => b
    .Handler("mod-bonus", session => {
        // add 15 instead
        return LSProcessResultStatus.SUCCESS;
    })
);

// Result: Only local "mod-bonus" (+15) executes
```

### ReadOnly Nodes: IMMUTABLE

Nodes marked `readonly` cannot be overridden by lower-priority contexts:

```csharp
// Global: mark critical handler as readonly
manager.Register<MyProcess>(root => root
    .Sequence("audit", seq => seq
        .Handler("log-transaction", LogHandler, readOnly: true)  // Cannot be overridden
    )
);

// Instance: attempting to override readonly handler is silently ignored
manager.Register<MyProcess>(root => root
    .Handler("log-transaction", session => {
        // This will NOT override the readonly global handler
        return LSProcessResultStatus.SUCCESS;
    })
, entity);

// Local: WithProcessing() also respects readonly
process.WithProcessing(b => b
    .Handler("log-transaction", session => {
        // This will NOT override the readonly handler
        return LSProcessResultStatus.SUCCESS;
    })
);
```

## ProcessInstanceBehaviour Flags

Controls which instance contexts are merged during `GetRootNode()`:

```csharp
public enum ProcessInstanceBehaviour {
    LOCAL = 0,              // Local context always merged (default)
    GLOBAL = 0 << 1,        // Include global context
    MATCH_INSTANCE = 0 << 2, // Use FIRST instance with registered context
    MULTI_INSTANCES = 0 << 3,// Use ALL instances with registered contexts
    ANY = GLOBAL | MATCH_INSTANCE,      // Global + first matching instance
    ALL = GLOBAL | MULTI_INSTANCES,     // Global + all matching instances
}
```

### Usage Examples

```csharp
// Single-target execution: use global + first instance that matches
var result = process.Execute(manager, ProcessInstanceBehaviour.ANY, entity1, entity2, entity3);
// Merges: Global → First matching instance (entity1 or entity2 or entity3) → Local

// Multi-target execution: use global + all provided instances
var result = process.Execute(manager, ProcessInstanceBehaviour.ALL, entity1, entity2);
// Merges: Global → Instance(entity1) → Instance(entity2) → Local

// Global-only: ignore instance contexts
var result = process.Execute(manager, ProcessInstanceBehaviour.GLOBAL);
// Merges: Global → Local
```

## Node Manipulation: Add, Remove, Override

### Adding Handlers Dynamically

```csharp
var process = new AttributeRecomputeProcess(entity, definer, world);
process.WithProcessing(b => b
    .Sequence("recompute", seq => seq
        .Handler("new-step", NewHandler)  // Appends to sequence
    )
);
```

### Removing Handlers (Via Override)

```csharp
// Override with empty decorator to "remove" children
process.WithProcessing(b => b
    .Sequence("cascade", seq => seq
        // Omit unwanted children; decorator MERGE means only listed children execute
    )
);
```

### Replacing Handlers

```csharp
// Handler with same ID overrides globally
process.WithProcessing(b => b
    .Handler("apply-modifiers", session => {
        // New implementation replaces global version
        return LSProcessResultStatus.SUCCESS;
    })
);
```

### Changing Decorator Strategy

```csharp
// Global: sequential cascade (safe, ordered)
manager.Register<MyProcess>(root => root
    .Sequence("cascade-deps", seq => seq
        .Handler("dep-1", UpdateDep1)
        .Handler("dep-2", UpdateDep2)
    )
);

// Instance: parallel cascade (fast, concurrent)
manager.Register<MyProcess>(root => root
    .Parallel("cascade-deps", par => par  // Same ID, decorator MERGE
        .Handler("dep-1", UpdateDep1)
        .Handler("dep-2", UpdateDep2),
        numRequiredToSucceed: 2,
        numRequiredToFailure: 1
    )
, vipEntity);

// Result for VIP: handlers from both sequences merged into Parallel node
```

## Thread Safety

LSProcessManager uses `ConcurrentDictionary` for thread-safe concurrent access:

```csharp
// Safe: Multiple threads can register simultaneously
Task.Run(() => manager.Register<Process1>(builder1));
Task.Run(() => manager.Register<Process2>(builder2));
Task.Run(() => manager.Register<Process1>(builder3, entity1)); // Different instance
Task.Run(() => var root = manager.GetRootNode(typeof(Process1), ...));
```

**Guarantees:**
- Registration operations are atomic per (ProcessType, Instance) pair
- GetRootNode() does not block other registrations
- Context cloning prevents original registrations from being modified

## Context Cloning Strategy

When merging, all contexts are cloned to preserve originals:

```csharp
// Original global context stored in manager
var globalNode = new LSProcessNodeSequence("seq", 0);

// During GetRootNode(), a CLONE is created and merged
var clonedGlobal = globalNode.Clone();  // Deep copy
builder.Merge(clonedGlobal);

// Original globalNode remains unchanged for future executions
var anotherProcess = manager.GetRootNode(typeof(MyProcess), ...);
// Uses fresh clone of original globalNode
```

**Benefits:**
- Registrations are immutable once stored
- Multiple concurrent executions don't interfere
- Predictable behavior across process instances

## Best Practices

1. **Register global defaults early** - Set up base behavior during initialization, not per-process
2. **Use instance contexts for differentiation** - Avoid duplicate code by registering variants once
3. **Reserve WithProcessing() for runtime tweaks** - Temporary buffs, test overrides, dynamic effects
4. **Mark immutable handlers as readonly** - Protect critical audit, validation, or state-mutation steps
5. **Choose explicit decorator strategy** - Use Sequence for safe ordered updates, Parallel for independent concurrent updates
6. **Batch registrations per entity type** - Register all modifiers for an entity class at startup, not per-instance
7. **Use consistent node IDs** - Enables reliable override/merge behavior across contexts
8. **Avoid deep nesting** - Keep decorator depth shallow for readability and performance
9. **Document definer-driven contexts** - If definers self-register, document the process in comments
10. **Test merge behavior** - Verify global + instance + local combinations work as expected

## Common Patterns

### Global Modifiers + Instance Overrides

```csharp
// Setup: Register all possible modifiers globally
foreach (var mod in allModifiers) {
    manager.Register<AttrProcess>(root => root
        .Handler($"mod-{mod.ID}", session => ApplyModifier(mod, session))
    );
}

// Customize: For specific entity, override one modifier
var bonusEntity = new BonusEntity();
manager.Register<AttrProcess>(root => root
    .Handler($"mod-bonus", session => ApplyBonusModifier(session)) // Override
, bonusEntity);

// Use: Execute with proper behavior merging
var process = new AttrProcess(bonusEntity, attr, world);
process.Execute(manager, ProcessInstanceBehaviour.ALL, bonusEntity);
```

### Safe Cascading with Sequential → Parallel Upgrade

```csharp
// Phase 1: Global sequential (safe, slow)
manager.Register<AttrProcess>(root => root
    .Sequence("cascade", seq => seq.Handler("dep-1", ...).Handler("dep-2", ...))
);

// Phase 2: For high-performance entities, upgrade to parallel
var highPerfEntity = new HighPerfEntity();
manager.Register<AttrProcess>(root => root
    .Parallel("cascade", par => par.Handler("dep-1", ...).Handler("dep-2", ...))
, highPerfEntity);
```

### Definer-Driven Registration

```csharp
// Definers self-register their behavior
public interface IAttributeModifierDefiner {
    void RegisterWithManager(LSProcessManager manager);
}

// During initialization
foreach (var definer in definers) {
    definer.RegisterWithManager(manager);  // Definer builds its own handler context
}

// Result: Manager acts as a registry, definers control their execution shape
```

## Troubleshooting

**Issue: Handler not executing**
- Check: Is ID correct? Is it readonly in a higher-priority context? Is it in a failed decorator?

**Issue: Expected children missing**
- Check: Are you using a decorator? Decorators MERGE (combine) children, not replace them.

**Issue: Wrong instance context applied**
- Check: ProcessInstanceBehaviour flags. Use `MATCH_INSTANCE` for single, `MULTI_INSTANCES` for all.

**Issue: Performance degradation with many instances**
- Check: Register common behavior globally, not per-instance. Batch registrations during init.

**Issue: Circular dependency or re-entry**
- Check: Mark readonly handlers, use cycle detection in handlers, check `IsCancelled` status.
