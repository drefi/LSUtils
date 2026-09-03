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
- **LSProcessSession**: execution context supplied to handlers, including Process.
  This is an internal operation context, not a game/world session.
- **Handler**: callback returning a status. A callback can observe data without
  changing it; returning FAILURE or WAITING also controls execution.

No C# events, output polling, or result consumer queue are required to register
an intervention. The caller still owns the invocation and lifetime of the process.

## Nodes

| Node | Role |
| --- | --- |
| Handler | Invoke one delegate and retain its result. |
| Sequence | Execute eligible children in order; continue on SUCCESS. |
| Selector | Try eligible children in order; continue on FAILURE. |
| Inverter | Reverse SUCCESS/FAILURE of its child; preserve other statuses. |

Priority orders siblings before insertion order. Conditions determine eligibility,
not an automatic business rejection: use a handler returning FAILURE for rejection.
A Selector is a choice of strategies, not a broadcast to observers.

Parallel and its thresholds have been removed. Execution happens synchronously
on the calling thread until completion or WAITING. Independent process instances
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
        return LSProcessResultStatus.SUCCESS;
    }));
manager.Register<RequestProcess>(b => b
    .Handler("adjust", s => {
        var value = s.Process.GetData<int>("result");
        s.Process.SetData("result", value + 2);
        return LSProcessResultStatus.SUCCESS;
    }));
manager.Register<RequestProcess>(b => b
    .Handler("observe", s => {
        ApplyResult(s.Process.GetData<int>("result"));
        return LSProcessResultStatus.SUCCESS;
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

## Waiting and control

A handler returns WAITING to suspend its branch. Later, an external owner may
update process data and call:

- Resume(): resolve the current waiting handler as SUCCESS and continue.
- Fail(): resolve it as FAILURE; a Selector can then try its fallback.
- Cancel(): cancel the pending execution.

Resume does not invoke the waiting delegate again. Put response validation in
the next handler if the external response needs validation. Resume is not a tick,
retry, or animation update. Another Execute() returns the current status without
starting the operation again; create another process for another operation.

For example, a registered "approval" handler may store the process reference in
an interaction controller and return WAITING. That controller later writes the
answer and calls Resume(). The following "validate-answer" and "apply" handlers
decide whether to continue. Resume should occur after Execute has returned WAITING;
reentrant completion from inside the waiting callback is not this protocol.

A Sequence's "completed" stage is reached only after earlier success. It is not
a finally block or a notification guaranteed for FAILURE/CANCELLED/exceptions.
Model alternative outcomes explicitly, or have the invocation owner inspect the
returned status. An observer returning FAILURE can prevent later observers from
running; return SUCCESS when observation must not veto the operation.

## Migration

- Remove Parallel builders, threshold configuration, and corresponding policies.
- Use Sequence only when steps depend on successful predecessors, or Selector
  for alternatives. Neither is a drop-in threshold aggregation replacement.
- Resume/Fail no longer accept node identifiers: there is one active waiting
  branch. SplitNode and dotted-path routing were removed.
- Existing numeric values of the remaining root-node enum members are preserved.

## Current limits and next improvements

The execution tree is intended to be configured before execution. WithProcessing
rejects changes afterward, but direct access to session nodes is not yet a complete
immutability boundary. Do not mutate those nodes while executing.

Exception handling and cleanup are not yet a comprehensive failure protocol.
Do not assume arbitrary delegate exceptions become FAILURE or trigger cleanup.
The flow-audit tests cover Selector exhaustion when resuming its last inverted
waiting child, including propagation to a parent fallback. Exception handling and
structural immutability remain separate work.

Recommended next steps, separately scoped:
1. Define exception propagation and session cleanup guarantees.
2. Enforce the boundary between editable templates and execution trees.
3. Validate named extension stages in the White Horse intent/action prototype,
   including rejection, suspended completion, and multiple observers.

The ProcessSystem NUnit suite contains executable examples; the observer/wait
contract is covered in ProcessInterventionTests.cs.
