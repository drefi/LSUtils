# LSEventSystem v5 - Improved Architecture Proposal

## Current Problems

1. **Race Conditions**: Resume() vs Process() concurrency issues
2. **Complex Waiting Logic**: Multiple states tracking waiting handlers
3. **Pseudo-Sequential Complexity**: Handler calling Resume() during execution
4. **State Coordination**: BusinessState and PhaseState both managing state

## Proposed Solution: Event-Driven State Machine

### Core Principles

1. **Single Processing Thread**: Only one operation processes phases at a time
2. **Event-Driven Transitions**: Use internal events for state changes
3. **Centralized Waiting Management**: BusinessState owns all waiting logic
4. **Simplified Phase Interface**: Phases only execute handlers, don't manage state

### New Architecture

```text
ProcessContext -> BusinessState -> EventProcessor
                                     |
                    Phase Queue: [VALIDATE, CONFIGURE, EXECUTE, CLEANUP]
                                     |
                    Handler Execution Engine
                                     |
                    Event Bus: [PhaseComplete, HandlerWaiting, ResumeRequested]
                                     |
                    State Transition Engine -> [Succeed, Failed, Cancelled, Completed]
```

### Updated Key Components

#### 1. Phase-Aware State Manager

```csharp
public class PhaseStateManager {
    private readonly Dictionary<PhaseType, IPhaseExecutor> _phaseExecutors;
    private readonly PseudoSequentialTracker _pseudoTracker;
    
    public PhaseResult ExecutePhase(PhaseType phase, List<Handler> handlers);
    public PhaseResult ResumePhase(PhaseType phase, string handlerId);
    public bool IsPseudoSequential(string handlerId);
}

public enum PhaseType {
    VALIDATE,   // Sequential, WAITING=FAILURE, stop on first failure
    CONFIGURE,  // Sequential, supports waiting/resume, stop on first waiting
    EXECUTE,    // Parallel-style, supports waiting/resume, run all unless cancelled
    CLEANUP     // Sequential, cancellation stops processing, doesn't affect final result
}
```

#### 2. Pseudo-Sequential Tracker

```csharp
public class PseudoSequentialTracker {
    private readonly ConcurrentDictionary<string, ResumeTimestamp> _resumeCalls;
    private readonly ConcurrentDictionary<string, WaitingTimestamp> _waitingHandlers;
    
    // Called when handler calls evt.Resume() during execution
    public void RecordResume(string handlerId, DateTime timestamp);
    
    // Called when handler returns WAITING
    public void RecordWaiting(string handlerId, DateTime timestamp);
    
    // Check if Resume was called before handler returned WAITING
    public bool IsPseudoSequential(string handlerId);
    
    // Get handlers that were pre-resumed
    public List<string> GetPreResumedHandlers();
}
```

#### 3. Phase-Specific Executors

```csharp
public interface IPhaseExecutor {
    PhaseResult ExecuteHandlers(List<Handler> handlers, ExecutionContext context);
    PhaseResult Resume(string handlerId, ExecutionContext context);
    bool SupportsWaiting { get; }
    bool StopsOnFirstWaiting { get; }
}

public class ValidatePhaseExecutor : IPhaseExecutor {
    public bool SupportsWaiting => false; // WAITING treated as FAILURE
    public bool StopsOnFirstWaiting => true;
    
    public PhaseResult ExecuteHandlers(List<Handler> handlers, ExecutionContext context) {
        foreach (var handler in handlers) {
            var result = handler.Execute(context);
            if (result == HandlerResult.FAILURE || result == HandlerResult.WAITING) {
                return PhaseResult.FAILURE; // Stop immediately, skip to completed
            }
        }
        return PhaseResult.SUCCESS;
    }
}

public class ConfigurePhaseExecutor : IPhaseExecutor {
    public bool SupportsWaiting => true;
    public bool StopsOnFirstWaiting => true;
    
    public PhaseResult ExecuteHandlers(List<Handler> handlers, ExecutionContext context) {
        var results = new List<HandlerResult>();
        
        foreach (var handler in handlers) {
            var result = handler.Execute(context);
            results.Add(result);
            
            if (result == HandlerResult.WAITING) {
                // Check if Resume was called during handler execution
                if (context.PseudoTracker.IsPseudoSequential(handler.Id)) {
                    // Treat as SUCCESS and continue
                    results[results.Count - 1] = HandlerResult.SUCCESS;
                    continue;
                }
                // True waiting - pause processing
                return PhaseResult.WAITING;
            }
            if (result == HandlerResult.CANCELLED) {
                return PhaseResult.CANCELLED;
            }
        }
        
        // Phase succeeds if at least one SUCCESS or not all FAILURE
        return results.All(r => r == HandlerResult.FAILURE) ? 
               PhaseResult.FAILURE : PhaseResult.SUCCESS;
    }
}

public class ExecutePhaseExecutor : IPhaseExecutor {
    public bool SupportsWaiting => true;
    public bool StopsOnFirstWaiting => false; // Run all handlers
    
    public PhaseResult ExecuteHandlers(List<Handler> handlers, ExecutionContext context) {
        var results = new List<HandlerResult>();
        var waitingHandlers = new List<string>();
        
        // Execute all handlers (parallel-style)
        foreach (var handler in handlers) {
            var result = handler.Execute(context);
            results.Add(result);
            
            if (result == HandlerResult.CANCELLED) {
                return PhaseResult.CANCELLED; // Only cancellation stops processing
            }
            if (result == HandlerResult.WAITING) {
                if (!context.PseudoTracker.IsPseudoSequential(handler.Id)) {
                    waitingHandlers.Add(handler.Id);
                }
            }
        }
        
        // If any handlers are truly waiting (not pseudo-sequential)
        if (waitingHandlers.Count > 0) {
            return PhaseResult.WAITING;
        }
        
        // Determine success based on collective results
        return EvaluateExecuteResults(results);
    }
}

public class CleanupPhaseExecutor : IPhaseExecutor {
    public bool SupportsWaiting => true;
    public bool StopsOnFirstWaiting => false;
    
    public PhaseResult ExecuteHandlers(List<Handler> handlers, ExecutionContext context) {
        foreach (var handler in handlers) {
            var result = handler.Execute(context);
            
            if (result == HandlerResult.CANCELLED) {
                // Stop processing, continue state flow
                // But don't change BusinessState outcome
                return PhaseResult.SUCCESS; // Cleanup doesn't affect final result
            }
        }
        return PhaseResult.SUCCESS; // Cleanup is always "successful" for state flow
    }
}
```

### Phase-Specific Waiting Behaviors

#### VALIDATE Phase

- **Handler Failure**: Any FAILURE → immediate PHASE FAILURE → skip to CompletedState
- **Handler Waiting**: Treat WAITING as FAILURE (no recovery for now)
- **Processing Model**: Sequential, stop on first failure/waiting
- **Phase Success**: All handlers return SUCCESS
- **Resume Logic**: Not applicable (WAITING treated as FAILURE)

#### CONFIGURE Phase

- **Handler Failure**: Record FAILURE, continue to next handler
- **Handler Waiting**: Exit Dispatch() with EventResult.WAITING
- **Processing Model**: Sequential, stop on first WAITING
- **Phase Success**: At least one SUCCESS OR all handlers processed without all being FAILURE
- **Resume Logic**:
  - Resume() called after handler returns WAITING → normal resume
  - Resume() called before handler finishes → "already resumed" (pseudo-sequential)
  - Continue processing remaining handlers after resume

#### EXECUTE Phase

- **Handler Failure**: Record FAILURE, continue to next handler
- **Handler Waiting**: Record WAITING, continue to next handler
- **Handler Cancelled**: ONLY cancellation stops processing, exit immediately
- **Processing Model**: Parallel-style (all handlers run unless cancelled)
- **Phase Success**: Based on collective results after all handlers complete
- **Resume Logic**:
  - Track multiple Resume() calls (can be called before handlers finish)
  - Continue only when all waiting handlers are resumed
  - Support pseudo-sequential (Resume() during handler execution)

#### CLEANUP Phase

- **Handler Failure**: Record FAILURE, continue to next handler
- **Handler Waiting**: Record WAITING, continue to next handler
- **Handler Cancelled**: Stop processing immediately, continue state flow
- **Processing Model**: Sequential until cancellation
- **Phase Success**: Doesn't affect BusinessState outcome (cleanup is best-effort)
- **Resume Logic**: Support waiting but doesn't change final BusinessState result

### Execution Flow

#### Normal Processing

1. `BusinessState.Process()` → Phase-specific execution
2. Phase executes handlers according to its processing model
3. Phase evaluates results according to its success criteria
4. Phase returns next state or WAITING based on results
5. BusinessState transitions accordingly

#### Resume Processing (CONFIGURE/EXECUTE phases)

1. `BusinessState.Resume()` → Current phase's Resume()
2. Phase checks if Resume() was called before handler finished (pseudo-sequential)
3. If pseudo-sequential: Mark handler as "already resumed", continue processing
4. If normal resume: Continue from where processing was paused
5. Phase continues with remaining handlers or completes

#### Pseudo-Sequential Handling

**The Key Innovation**: A handler calls `evt.Resume()` during its own execution

**CONFIGURE Phase (Sequential)**:

1. Handler1 starts execution
2. Handler1 calls `evt.Resume()` during execution
3. Handler1 returns WAITING
4. System detects pseudo-sequential case (Resume before WAITING)
5. Treats Handler1 as SUCCESS, continues to Handler2
6. Dispatch() returns SUCCESS (not WAITING)

**EXECUTE Phase (Parallel-style)**:

1. All handlers start execution
2. Some handlers call `evt.Resume()` during execution
3. Some handlers return WAITING
4. System detects pseudo-sequential cases
5. Marks those handlers as "already resumed"
6. Waits only for handlers that didn't call Resume()
7. Dispatch() returns SUCCESS when all are resolved

### Concrete Example: Pseudo-Sequential Flow

#### Test Scenario (ConfigurePhaseSequentialWaiting_PseudoSequential)

```csharp
// Handler setup
Handler1: returns WAITING, but calls evt.Resume() during execution
Handler2: returns SUCCESS

// Expected behavior:
// 1. Handler1 executes and calls evt.Resume()
// 2. Handler1 returns WAITING
// 3. System detects pseudo-sequential case
// 4. Handler1 treated as SUCCESS
// 5. Handler2 executes
// 6. Phase result: SUCCESS
// 7. Dispatch() result: SUCCESS (not WAITING)
```

#### Step-by-Step Execution

```text
1. ConfigurePhase.Process() starts
2. Handler1.Execute() begins
   - PseudoTracker.RecordHandlerStart("handler1", timestamp)
   
3. Inside Handler1.Execute():
   - Handler calls evt.Resume()
   - PseudoTracker.RecordResume("handler1", timestamp)
   
4. Handler1.Execute() returns WAITING
   - PseudoTracker.RecordWaiting("handler1", timestamp)
   - ConfigurePhaseExecutor checks: IsPseudoSequential("handler1") → TRUE
   - Treats Handler1 as SUCCESS, continues processing
   
5. Handler2.Execute() runs and returns SUCCESS

6. ConfigurePhase.Process() completes with SUCCESS
   - No true waiting handlers remain
   - Returns ExecutePhaseState (not this)

7. BusinessState.Process() continues to Execute phase
8. Eventually returns SucceedState
9. Dispatch() returns EventProcessResult.SUCCESS
```

#### Key Innovation - Timing Detection //DEV COMMENT: IsPseudoSequential don't make much sense

```csharp
public bool IsPseudoSequential(string handlerId) {
    if (!_resumeCalls.TryGetValue(handlerId, out var resumeTime) ||
        !_waitingHandlers.TryGetValue(handlerId, out var waitingTime)) {
        return false;
    }
    
    // If Resume was called before (or very close to) WAITING return
    // this indicates the handler called Resume during its execution
    return resumeTime.Timestamp <= waitingTime.Timestamp.AddMilliseconds(10);
}
```

### Benefits of This Approach

1. **Deterministic**: Clear timing-based detection of pseudo-sequential behavior
2. **Phase-Specific**: Each phase handles waiting according to its specific rules
3. **No Race Conditions**: PseudoTracker handles concurrency with timestamps
4. **Testable**: Each component can be unit tested independently
5. **Clear Separation**: Business logic vs timing logic vs phase execution logic
6. **Maintainable**: Easy to understand and modify phase-specific behaviors

### Migration Strategy

1. Keep existing interfaces for backward compatibility
2. Implement new EventProcessor behind existing BusinessState
3. Gradually migrate phases to simplified interface
4. Update tests to match new deterministic behavior
5. Remove old complex logic once migration complete

### Phase Implementation Example

```csharp
public class ConfigurePhaseState : PhaseState {
    public override PhaseExecutionResult ExecuteHandlers() {
        var results = new List<HandlerResult>();
        
        // Execute handlers sequentially (for Configure)
        foreach (var handler in _handlers) {
            var result = handler.Execute(_context);
            results.Add(result);
            
            // No complex logic - just collect results
            if (result.Result == HandlerProcessResult.WAITING) {
                _eventBus.Publish(InternalEvent.HandlerWaiting, 
                    new HandlerWaitingEvent(handler.Id, _context));
            }
            
            // Stop on first waiting for sequential phases
            if (result.Result == HandlerProcessResult.WAITING) break;
        }
        
        return new PhaseExecutionResult(results);
    }
}
```

Would you like me to implement this new architecture? It would solve the current issues and provide a much cleaner, more maintainable solution.
