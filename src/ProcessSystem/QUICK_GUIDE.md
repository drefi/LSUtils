# LSProcessSystem

LSProcessSystem composes an execution tree for a single operation. External
participants register handlers through LSProcessManager to inspect data, change
a decision, reject work, or suspend completion. It is not a thread scheduler,
an update loop, or a general event bus.

## When to use it

Use a process when an operation has meaningful extension points and participants
must share its data or influence its outcome:

- Validate a request before applying it.
- Resolve an intent, execute its action, then let adapters inspect the result.
- Add optional steps for a specific entity using an ILSProcessable context.
- Suspend a workflow for a user response, then continue its remaining steps.
- Try a fallback when an earlier strategy fails.

Prefer an ordinary method for a fixed operation with no intervention points.
Continuous movement, clocks, queues, and timer scheduling remain the caller's
responsibility. A process may wait for a timer's owner; it does not measure time.

## Responsibilities

- **LSProcess**: one operation, its data, and its base tree via processing().
  Use typed properties for a stable contract or SetData/GetData for agreed keys.
- **LSProcessManager**: registered tree fragments by process type and optionally
  by ILSProcessable instance. Register before executing the operation.
- **LSProcessTreeBuilder**: named nodes and composition policies.
- **LSProcessDefinition**: immutable result of composition, with no execution state.
- **LSProcessSession**: operation context supplied to handlers, including Process.
  RootNode/CurrentNode expose read-only execution nodes; typed contexts share the
  original execution and SessionID. This is not a game/world session.
- **Handler**: callback returning an LSProcessResult. A callback can observe data
  without changing it; failure and waiting results also control execution.

No C# events, output polling, or result consumer queue are required to register
an intervention. The caller still owns the invocation and lifetime of the process.

## Nodes

| Node | Role |
| --- | --- |
| Handler | Invoke one delegate and retain its result. |
| Sequence | Execute eligible children in order; continue on success. |
| Selector | Try eligible children in order; continue on failure. |
| Inverter | Reverse success/failure of its child; preserve other outcomes. |

Empty composites have explicit identities: an empty Sequence succeeds, an empty
Selector fails, and an empty Inverter is Undetermined because no child result
exists to invert.

Priority orders siblings before insertion order. Conditions determine eligibility,
not an automatic business rejection: use a handler returning failure for rejection.
A Selector is a choice of strategies, not a broadcast to observers.

Parallel and its thresholds have been removed. Execution happens synchronously
on the calling thread until completion or a waiting result. Independent process instances
do not imply thread safety for shared callbacks or data.

## Observer-style intervention

This example registers three ordered participants without direct references
between them. The final participant receives the value changed by the second:

```csharp
sealed class RequestProcess : LSProcess { }

var manager = new LSProcessManager();
manager.Register<RequestProcess>(b => b
    .Handler("resolve", s => {
        s.Process.SetData("result", 10);
        return LSProcessResult.Success;
    }));
manager.Register<RequestProcess>(b => b
    .Handler("adjust", s => {
        var value = s.Process.GetData<int>("result");
        s.Process.SetData("result", value + 2);
        return LSProcessResult.Success;
    }));
manager.Register<RequestProcess>(b => b
    .Handler("observe", s => {
        ApplyResult(s.Process.GetData<int>("result"));
        return LSProcessResult.Success;
    }));

var process = new RequestProcess();
var status = process.Execute(manager);
```

For larger contracts, define named stages such as "validate", "resolve", "apply",
and "completed" in the base tree, then register handlers inside those stages.
This keeps semantic order explicit instead of relying on registration order.
Node IDs identify composition points; they are not execution paths.

Composition merges registered trees with the local tree. Same-name nodes can be
merged or replaced according to NodeUpdatePolicy; do not assume registration is
simple callback append. Test the chosen merge policies and ordering. To limit a
registration to an object, supply its ILSProcessable instance to Register and
include it among the contexts passed to Execute.

## Typed results

LSProcessResult separates flow outcome from optional detail. Static values represent
outcomes without data; Succeeded, Failed, WaitingFor, CancelledBy, and UndeterminedBy
create results with typed payloads. Inspect them with TryGetPayload<T> or
GetPayload<T>; the latter throws when the payload is absent or incompatible.

Flow equality intentionally compares only the outcome. NotExecuted is the initial
execution-node state, while Undetermined means execution produced no supported
terminal result. Sequence preserves its last successful payload, Selector its
decisive success or final failure payload, and Inverter carries the original result
inside LSProcessInversion rather than reinterpreting the payload.

## Waiting and control

A handler returns a waiting result to suspend its branch. Later, an external owner may
update process data and call:

- Resume(): resolve the current waiting handler as success and continue.
- Fail(): resolve it as failure; a Selector can then try its fallback.
- Cancel(): cancel the pending execution.

These operations also accept typed payloads. Cancel(reason) returns the resulting
typed cancellation; parameterless LSProcess.Cancel() remains void for method-group
compatibility.

Resume and Fail only affect an active waiting handler. After any other outcome they
return the existing result unchanged. Explicit cancellation remains allowed after
completion and replaces the root outcome, preserving an optional cancellation payload.

Resume does not invoke the waiting delegate again. Put response validation in
the next handler if the external response needs validation. Resume is not a tick,
retry, or animation update. Another Execute() returns the current status without
starting the operation again; create another process for another operation.

For example, a registered "approval" handler may store the process reference in
an interaction controller and return WAITING. That controller later writes the
answer and calls Resume(). The following "validate-answer" and "apply" handlers
decide whether to continue. Resume should occur after Execute has returned WAITING;
reentrant completion from inside the waiting callback is not this protocol.
Execute, Resume, and Fail are rejected while the same execution is already running.
A handler may call session.Cancel(reason); cancellation wins over the handler's return
value and prevents remaining handlers from running.

A Sequence's "completed" stage is reached only after earlier success. It is not
a finally block or a notification guaranteed for failure, cancellation, or exceptions.
Model alternative outcomes explicitly, or have the invocation owner inspect the
returned result. An observer returning failure can prevent later observers from
running; return success when observation must not veto the operation.

## Migration

- Remove Parallel builders, threshold configuration, and corresponding policies.
- Use Sequence only when steps depend on successful predecessors, or Selector
  for alternatives. Neither is a drop-in threshold aggregation replacement.
- Resume/Fail no longer accept node identifiers: there is one active waiting
  branch. SplitNode and dotted-path routing were removed.
- LSProcessResultStatus was replaced by LSProcessResult. LSProcessPriority is now
  an ordered value object; named values and default NORMAL behavior remain.
- LSProcessRootNodeType was unused and removed. NodeUpdatePolicy and
  LSProcessContextMode remain flags; LSProcessDefinitionNodeKind remains a closed
  structural discriminator.
- ILSProcessNode/ILSProcessLayerNode now describe editable templates only. They
  no longer expose Execute/Resume/Fail/Cancel, statuses, or execution counters.
- Inspect session.RootNode or session.CurrentNode for runtime status. These are
  LSProcessExecutionNode objects, not editable templates; inspect Definition for
  immutable metadata and Children for read-only child states.
- ExecutionCount now counts completed handler invocations in this execution,
  not aggregate invocations through template clones. Aggregate telemetry belongs
  to an explicit observer if needed, not to shared mutable definition metadata.

## Current limits and next improvements

The final definition is copied after local/built-in/instance/global composition,
before any handler or condition executes. WithProcessing rejects changes after
execution starts. Retaining a builder or mutating manager registrations cannot
change an existing definition, including branches not yet visited.

Delegate exceptions propagate and restore CurrentNode. The execution retains the
exception and rejects further execution/continuation by rethrowing it, preventing
partial work from being repeated. This is not a comprehensive failure/recovery
protocol: there is no automatic conversion to failure, rollback, or domain cleanup.
Repeated LSProcess.Execute still returns the current status as before.

Undetermined remains indeterminate; it is not reported as success merely because a
sequence exhausted its children. Resume/Fail only resolve an actual WAITING.
Explicit Cancel remains supported after completion, matching the existing contract.

The incremental definition/execution migration is tracked in
[IMMUTABILITY_ROADMAP.md](IMMUTABILITY_ROADMAP.md), including compatibility notes
and a diagnostic allocation/timing comparison. The new executor is integrated;
the previous executor has been removed.

Recommended next steps, separately scoped:
1. Define exception propagation and session cleanup guarantees.
2. Consider explicit custom-node support if a concrete use case requires it.
3. Validate named extension stages in the White Horse intent/action prototype,
   including rejection, suspended completion, and multiple observers.

The ProcessSystem NUnit suite contains executable examples; the observer/wait
contract is covered in ProcessInterventionTests.cs. Nested payload propagation,
terminal interventions, reentrancy, condition faults, and empty composites are
covered in ProcessResultContractTests.cs.
