# Definition / execution separation

## Contract

Keep the four composition sources: WithProcessing, processing override, selected
ILSProcessable registrations, and enabled global registration. Preserve actual
merge behavior and NodeUpdatePolicy, not a newly invented precedence hierarchy.
The manager remains an editable template store. Each execution owns its data.

Freeze only after composition, before invoking handlers or conditions. Conditions
remain runtime decisions. Immutability protects topology/configuration, not objects
captured by delegates. This design does not promise thread-safe registration or
execution. No events, scheduler, cache, or new update mechanism are introduced.

## Incremental deliveries

1. Completed: internal immutable definitions and compilation from existing nodes.
   Tests: copied collections, nested topology, metadata, no callback evaluation,
   cycle rejection, and isolation from later manager registration.
   No production execution changes in this stage.
   Validation: 121 ProcessSystem NUnit tests passed, including four new definition tests.
2. Completed: characterize all composition sources together before integration.
   Include same-ID conflicts, policies, context modes, multiple instances, and
   registration changes while a previous execution is waiting.
   Nine new contract tests passed against the original executor before integration;
   the existing update-policy and condition tests also remain green.
3. Completed: implement per-execution state and interpretation of definitions.
   A 24-case differential matrix passed before removal of the original executor.
   Its traces/statuses are now explicit regression expectations. Additional tests
   cover conditions, shared-definition isolation, counters, and typed contexts.
4. Completed: integrate compilation after the existing merge in LSProcess.Execute.
   Migrate session and typed handler context to the shared execution state.
   Ensure no editable tree reference escapes into execution. Validate the White
   Horse intent/action prototype before retiring the original executor.
5. Completed: remove obsolete execution fields from composition nodes, finalize
   public APIs, and measure construction/continuation allocations and throughput.
   Definition caching remains deliberately deferred: reuse requires a complete
   composition identity and registration invalidation policy, not only process type.
6. Planned: migrate enum-based contracts toward stronger semantic types, with
   payload-bearing process results as the first priority. See the detailed stage below.
7. Planned: completely revise and reorganize ProcessSystem documentation against
   the current implementation, after the typed-result migration. See the final section.

## Final representation

LSProcessDefinition and LSProcessNodeDefinition are public, sealed, and contain
no status, cursor, stack, or execution counters. Collections are defensively copied
and wrapped read-only. Handler/condition delegates are retained without invocation.
Only the four built-in concrete node types are accepted; unknown subclasses are
rejected instead of silently losing overridden execution behavior. Extension-node
support remains deferred until an explicit contract is needed.

LSProcess.Execute composes in the original order, compiles the result, releases its
local template reference, and creates a session over the definition. The manager's
stored templates remain editable. Runtime uses no template execution methods.

LSProcessExecutionNode exposes Definition, Children, Status, and ExecutionCount
without editing or public execution operations. Session RootNode and CurrentNode
use this type. Session-level Execute/Resume/Fail/Cancel are the control boundary.
LSProcessSession<T> shares the original execution, identity, cursor, and context.

Counters are per-execution rather than shared through clone references. This is an
intentional API/semantics change; aggregate telemetry should use an explicit observer.
Handler and condition closures can still refer to mutable application objects;
structural immutability neither clones those objects nor makes them thread-safe.

Two narrow consistency guarantees accompany the new interpreter: UNKNOWN is not
converted into a successful return merely because a sequence exhausts its children,
and callback exceptions restore CurrentNode and are retained to prevent accidental
continuation of partial work. Exceptions still propagate, never become FAILURE
implicitly, and do not implement rollback. Broader exception policy remains future work.

## Verification and measurement

- 480 LSUtils NUnit tests passed after integration (162 in ProcessSystem).
- Three White Horse intent/action prototype tests passed; dependent projects built.
- The diagnostic performance test is Explicit and excluded from routine runs.
- Before removal, the differential matrix matched the original executor for all
  four node kinds with success, failure, waiting, cancellation, Resume, and Fail.

Diagnostic workload: 30 warmups, then 500 operations, each composed from a manager
template of 32 handlers with a WAITING at index 16, followed by Resume. Debug/net8.0,
same machine, no definition cache. Allocations use GC.GetAllocatedBytesForCurrentThread.

| Implementation | Total elapsed | Allocated bytes/operation |
| --- | ---: | ---: |
| Original executor | 156.53 ms | 93,729 |
| Definition + separate state | 72.11 ms | 28,452 |

This is a single local diagnostic sample, not a statistical benchmark or a frame
rate prediction. Results include removal of old per-node logging allocations and
repeated status aggregation, not merely immutability. No threshold is enforced in CI.
Run ProcessPerformanceTests.MeasureConstructionAndContinuation explicitly to repeat.

## Acceptance verified

- Existing composition and continuation behavior is preserved by NUnit tests.
- Two executions cannot share mutable execution state.
- Editing manager/local templates cannot change an already finalized definition.
- Retained arrays, child references, and casts cannot mutate a definition.
- Adapter callbacks still access the correct process/session and can wait or veto.
- Build and tests pass in LSUtils and the dependent White Horse prototype.
- Performance claims are based on measurements, not immutability alone.

## Planned stage 6: stronger types and detailed results

Replace enum-only contracts where they cannot express the domain invariants.
Prioritize LSProcessResultStatus: a status alone cannot explain a rejection,
identify the result producer, or carry a typed value for registered participants.
This stage is planned only; no result or enum API is changed yet.

1. Review the existing enums by responsibility: execution lifecycle, operation
   outcomes, node kinds, priority, and composition/context policies. Specify each
   replacement and its valid combinations rather than introducing one generic
   wrapper for every enum. Preserve flag semantics where independent options exist.
2. Define a strongly typed result contract with explicit variants and validated
   construction. Consider a generic success payload and typed failure/cancellation
   details, without requiring consumers to cast object values or inspect string keys.
   Type names and class/struct representation remain design decisions, not commitments.
3. Separate lifecycle from outcome: not executed, waiting, and indeterminate must
   not be ambiguous synonyms for UNKNOWN. Specify whether waiting carries a reason
   or continuation context without implying completion. Exceptions remain distinct
   from expected business failures unless an explicit conversion policy is adopted.
4. Formalize payload propagation through Sequence, Selector, and Inverter. Decide
   which child result a composite exposes, how fallback attempts retain diagnostic
   provenance, and what inversion means for typed payloads. Inverting success must
   not silently reinterpret a success value as failure details, or vice versa.
5. Define how injected handlers inspect and influence detailed results, including
   veto, replacement, and waiting before completion. Completed result values should
   be immutable; any permitted replacement must be explicit. Preserve the callback
   flow through ProcessManager rather than introducing events or result consumers.
6. Migrate handler signatures, execution state, session operations, logging, and
   dependent prototypes incrementally. Test heterogeneous handler payloads in one
   tree and typed session access; stronger typing must not require every node to
   return the same application-specific payload type.
7. Add NUnit coverage for variant invariants, payload propagation, empty composites,
   fallback diagnostics, inversion, waiting/resumption, cancellation, and repeated
   reads after completion. Compare allocations and throughput against this roadmap's
   baseline before choosing reference/value representations or retaining adapters.

Acceptance: consumers can distinguish control state from a detailed outcome and
access supported payloads safely; invalid state/payload combinations are prevented
by the contract; composition and continuation remain deterministic; any compatibility
changes and performance tradeoffs are documented before old enum APIs are removed.

## Planned final stage 7: documentation overhaul

Audit and rewrite the current ProcessSystem documentation using the implementation
and executable tests as evidence. Existing prose is not the source of truth when
it conflicts with behavior. This is a planned documentation project, not a claim
that the existing documentation has already been fully corrected.

1. Inventory README entry points, guides, API references, XML comments, examples,
   and dependent integration notes. Identify obsolete APIs, contradictory claims,
   duplicate explanations, broken links, and undocumented public behavior.
2. Organize documentation by responsibility: purpose and suitability; composition
   and registration; immutable definitions; execution/session lifecycle; node
   semantics; typed results and payload propagation; external interventions;
   diagnostics, limitations, migration, and performance measurement.
3. Explain all four composition sources, actual ordering and merge policies,
   context selection, and the effect of later registrations on future versus
   already-running processes. Separate composition metadata from execution state.
4. Document every supported node and control operation, including empty trees,
   conditions and priorities, waiting and continuation, cancellation, exceptions,
   repeated calls, and typed session views. Distinguish verified guarantees from
   known defects, intentionally unsupported behavior, and future proposals.
5. Provide runnable examples for observer-style callbacks, result inspection and
   modification, veto, fallback, and suspended completion through ProcessManager.
   Explain when a direct method is sufficient and when the process tree adds value.
   Do not describe the system as a thread scheduler, update loop, or event bus.
6. Reconcile XML documentation with signatures and behavior. Replace obsolete
   examples rather than preserving misleading compatibility descriptions. Move
   historical decisions to clearly labeled migration/history material and make
   the current guide the discoverable starting point from repository indexes.
7. Link examples and behavioral claims to NUnit coverage, add executable examples
   for uncovered contracts, validate documentation links, and review dependent
   prototype instructions. Record measurement conditions for performance claims.

Acceptance: current functionality is discoverable by topic, examples compile and
match tests, public contracts and limitations are explicit, and removed APIs or
planned features cannot be mistaken for supported functionality. Implementation
issues discovered during the audit must be reported separately, not silently
changed to fit the documentation. Future code changes must update the relevant
reference, example, and test together.
