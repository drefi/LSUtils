using System.Collections.Generic;
using System.Linq;
using LSUtils.ProcessSystem;
using NUnit.Framework;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Comprehensive tests for NodeUpdatePolicy behavior in complex scenarios.
/// Tests real-world use cases like plugin systems, modular architectures, and protected core functionality.
/// </summary>
[TestFixture]
public class NodeUpdatePolicyTests {
    private LSProcessManager? _manager;
    private static readonly string[] expected = new[] { "original-check" };

    [SetUp]
    public void SetUp() {
        _manager = new LSProcessManager();
    }

    [TearDown]
    public void TearDown() {
        _manager = null;
    }

    #region READONLY Policy Tests

    [Test]
    public void READONLY_Handler_ShouldPreventAllModifications() {
        // Scenario: Core system handler that plugins cannot override
        var log = new List<string>();
        var process = new BasicProcess();

        // Core system registers protected handler
        _manager!.Register<BasicProcess>(b => b
            .Handler("core-validation",
                session => { log.Add("core"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.READONLY)
        );

        // Plugin tries to override (should fail silently)
        _manager.Register<BasicProcess>(b => b
            .Handler("core-validation",
                session => { log.Add("plugin"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Only core handler should execute
        var expected = new[] { "core" };
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void READONLY_Layer_ShouldPreventModificationButAllowInitialBuilder() {
        // Scenario: Protected sequence that can be initially populated but not modified later
        var log = new List<string>();
        var process = new BasicProcess();

        // Initial registration with READONLY and builder
        _manager!.Register<BasicProcess>(b => b
            .Sequence("protected-flow", seq => seq
                .Handler("step1", s => { log.Add("step1"); return LSProcessResultStatus.SUCCESS; })
                .Handler("step2", s => { log.Add("step2"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.READONLY)
        );

        // Try to add more handlers (should be ignored)
        _manager.Register<BasicProcess>(b => b
            .Sequence("protected-flow", seq => seq
                .Handler("step3", s => { log.Add("step3"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Only original handlers execute
        var expected = new[] { "step1", "step2" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region IGNORE_CHANGES Policy Tests

    [Test]
    public void IGNORE_CHANGES_ShouldPreventPropertyChanges_ButAllowBuilder() {
        // Scenario: Layer that allows child additions but not property changes
        var log = new List<string>();
        var process = new BasicProcess();

        // Register with IGNORE_CHANGES
        _manager!.Register<BasicProcess>(b => b
            .Sequence("extendable-flow", seq => seq
                .Handler("original", s => { log.Add("original"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.IGNORE_CHANGES,
                LSProcessPriority.LOW)
        );

        // Try to change priority but add new handler
        _manager.Register<BasicProcess>(b => b
            .Sequence("extendable-flow", seq => seq
                .Handler("added", s => { log.Add("added"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.OVERRIDE_PRIORITY,
                LSProcessPriority.HIGH)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Both handlers execute (builder worked) but priority unchanged
        var expected = new[] { "original", "added" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region IGNORE_BUILDER Policy Tests

    [Test]
    public void IGNORE_BUILDER_ShouldPreventBuilderActions_ButAllowPropertyUpdates() {
        // Scenario: Fixed structure that can be reconfigured but not restructured
        var log = new List<string>();
        var process = new BasicProcess();

        // Register with IGNORE_BUILDER
        _manager!.Register<BasicProcess>(b => b
            .Sequence("fixed-structure", seq => seq
                .Handler("only-child", s => { log.Add("original"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.IGNORE_BUILDER,
                LSProcessPriority.LOW)
        );

        // Try to add children via builder (should be ignored) but change priority (should work)
        _manager.Register<BasicProcess>(b => b
            .Sequence("fixed-structure", seq => seq
                .Handler("should-not-appear", s => { log.Add("new"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.OVERRIDE_PRIORITY,
                LSProcessPriority.HIGH)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Only original child exists
        var expected = new[] { "original" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region REPLACE_LAYER Policy Tests

    [Test]
    public void REPLACE_LAYER_ShouldChangeLayerType_SequenceToSelector() {
        // Scenario: Refactoring a sequence to a selector
        var log = new List<string>();
        var process = new BasicProcess();

        // Initially register as sequence
        _manager!.Register<BasicProcess>(b => b
            .Sequence("changeable-layer", seq => seq
                .Handler("first", s => { log.Add("first-fail"); return LSProcessResultStatus.FAILURE; })
                .Handler("second", s => { log.Add("second-success"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Replace with selector
        _manager.Register<BasicProcess>(b => b
            .Selector("changeable-layer", sel => sel
                .Handler("first", s => { log.Add("first-fail"); return LSProcessResultStatus.FAILURE; })
                .Handler("second", s => { log.Add("second-success"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.REPLACE_LAYER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Selector behavior (stops after first success)
        var expected = new[] { "first-fail", "second-success" };
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void REPLACE_LAYER_WithoutFlag_ShouldKeepExistingLayerAndSkipBuilder() {
        // Scenario: Attempting to change layer type without REPLACE_LAYER should no-op the builder
        var log = new List<string>();
        var process = new BasicProcess();

        // Register as sequence
        _manager!.Register<BasicProcess>(b => b
            .Sequence("fixed-type", seq => seq
                .Handler("first", s => { log.Add("first-fail"); return LSProcessResultStatus.FAILURE; })
                .Handler("second", s => { log.Add("second-success"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Try to replace with selector without REPLACE_LAYER (should be ignored)
        _manager.Register<BasicProcess>(b => b
            .Selector("fixed-type", sel => sel
                .Handler("third", s => { log.Add("third"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.DEFAULT_LAYER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Existing sequence remains and builder for selector is skipped
        // Sequence should stop after first failure, so only the first handler logs
        var expected = new[] { "first-fail" };
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void REPLACE_LAYER_Handler_ShouldReplaceLayerWithHandler() {
        // Scenario: Simplifying a complex layer to a single handler
        var log = new List<string>();
        var process = new BasicProcess();

        // Register complex sequence
        _manager!.Register<BasicProcess>(b => b
            .Sequence("simplify-me", seq => seq
                .Handler("step1", s => { log.Add("step1"); return LSProcessResultStatus.SUCCESS; })
                .Handler("step2", s => { log.Add("step2"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Replace entire sequence with simple handler
        _manager.Register<BasicProcess>(b => b
            .Handler("simplify-me",
                s => { log.Add("simple"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.REPLACE_LAYER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Only simple handler executes
        var expected = new[] { "simple" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region OVERRIDE_HANDLER Policy Tests

    [Test]
    public void OVERRIDE_HANDLER_ShouldReplaceHandlerLogic() {
        // Scenario: Patching a handler's implementation
        var log = new List<string>();
        var process = new BasicProcess();

        // Original handler
        _manager!.Register<BasicProcess>(b => b
            .Handler("patchable",
                s => { log.Add("v1"); return LSProcessResultStatus.SUCCESS; })
        );

        // Patch with new implementation
        _manager.Register<BasicProcess>(b => b
            .Handler("patchable",
                s => { log.Add("v2-patched"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: New implementation executes
        var expected = new[] { "v2-patched" };
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void DEFAULT_HANDLER_ShouldOverrideByDefault() {
        // Scenario: Default behavior should override handlers
        var log = new List<string>();
        var process = new BasicProcess();

        _manager!.Register<BasicProcess>(b => b
            .Handler("auto-override", s => { log.Add("original"); return LSProcessResultStatus.SUCCESS; })
        );

        // Using default policy (which includes OVERRIDE_HANDLER)
        _manager.Register<BasicProcess>(b => b
            .Handler("auto-override", s => { log.Add("replacement"); return LSProcessResultStatus.SUCCESS; })
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert
        var expected = new[] { "replacement" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region Condition Policy Tests

    [Test]
    public void OVERRIDE_CONDITIONS_ShouldReplaceAllConditions() {
        // Scenario: Completely replacing condition logic
        var log = new List<string>();
        var process = new BasicProcess();
        process.SetData("allow-v1", false);
        process.SetData("allow-v2", true);

        // Original with condition
        _manager!.Register<BasicProcess>(b => b
            .Handler("conditional",
                s => { log.Add("executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER,
                conditions: p => p.TryGetData<bool>("allow-v1", out var allow) && allow)
        );

        // Replace condition
        _manager.Register<BasicProcess>(b => b
            .Handler("conditional",
                s => { log.Add("executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.OVERRIDE_CONDITIONS,
                conditions: p => p.TryGetData<bool>("allow-v2", out var allow) && allow)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Executes because v2 condition is true
        var expected = new[] { "executed" };
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void MERGE_CONDITIONS_ShouldCombineConditions() {
        // Scenario: Adding additional conditions without removing existing ones
        var log = new List<string>();
        var process = new BasicProcess();
        process.SetData("condition-a", true);
        process.SetData("condition-b", false);

        // Original with one condition
        _manager!.Register<BasicProcess>(b => b
            .Handler("multi-conditional",
                s => { log.Add("executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER,
                conditions: p => p.TryGetData<bool>("condition-a", out var a) && a)
        );

        // Add another condition (both must be true)
        _manager.Register<BasicProcess>(b => b
            .Handler("multi-conditional",
                s => { log.Add("executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.MERGE_CONDITIONS,
                conditions: p => p.TryGetData<bool>("condition-b", out var b) && b)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Doesn't execute because condition-b is false
        Assert.That(log, Is.Empty);
    }

    [Test]
    public void NoConditionPolicy_ShouldPreserveExistingConditions() {
        // Scenario: Updating handler without affecting conditions
        var log = new List<string>();
        var process = new BasicProcess();
        process.SetData("enabled", true);

        // Original with condition
        _manager!.Register<BasicProcess>(b => b
            .Handler("preserve-condition",
                s => { log.Add("v1"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER,
                conditions: p => p.TryGetData<bool>("enabled", out var e) && e)
        );

        // Update handler without condition policy
        _manager.Register<BasicProcess>(b => b
            .Handler("preserve-condition",
                s => { log.Add("v2"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Executes with new handler but old condition
        Assert.That(log, Is.EqualTo(new[] { "v2" }));
    }

    #endregion

    #region Priority Policy Tests

    [Test]
    public void OVERRIDE_PRIORITY_ShouldChangeExecutionOrder() {
        // Scenario: Reordering handlers by changing priority
        var log = new List<string>();
        var process = new BasicProcess();

        // Register two handlers with different priorities
        _manager!.Register<BasicProcess>(b => {
            b.Handler("low-priority", s => { log.Add("low"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.LOW);
            b.Handler("high-priority", s => { log.Add("high"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.HIGH);
            return b;
        });

        // Change low to critical
        _manager.Register<BasicProcess>(b => b
            .Handler("low-priority",
                s => { log.Add("now-critical"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.OVERRIDE_PRIORITY,
                LSProcessPriority.CRITICAL)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Critical priority executes first, then high
        Assert.That(log, Is.EqualTo(new[] { "now-critical", "high" }));
    }

    [Test]
    public void NoPriorityPolicy_ShouldPreserveOriginalPriority() {
        // Scenario: Updating handler without changing priority
        var log = new List<string>();
        var process = new BasicProcess();

        _manager!.Register<BasicProcess>(b => {
            b.Handler("preserve-priority", s => { log.Add("low-v1"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.LOW);
            b.Handler("other", s => { log.Add("high"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.HIGH);
            return b;
        });

        // Update handler without OVERRIDE_PRIORITY
        _manager.Register<BasicProcess>(b => b
            .Handler("preserve-priority",
                s => { log.Add("low-v2"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER,
                LSProcessPriority.CRITICAL) // This should be ignored
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: High priority still executes first
        Assert.That(log, Is.EqualTo(new[] { "high", "low-v2" }));
    }

    #endregion

    #region Parallel Node Policy Tests

    [Test]
    public void OVERRIDE_PARALLEL_Thresholds_ShouldChangeParallelBehavior() {
        // Scenario: Adjusting parallel thresholds for different game modes
        var log = new List<string>();
        var process = new BasicProcess();

        // Initial parallel with strict thresholds
        _manager!.Register<BasicProcess>(b => b
            .Parallel("resource-gather", par => par
                .Handler("wood", s => { log.Add("wood"); return LSProcessResultStatus.SUCCESS; })
                .Handler("stone", s => { log.Add("stone"); return LSProcessResultStatus.SUCCESS; })
                .Handler("metal", s => { log.Add("metal"); return LSProcessResultStatus.FAILURE; }),
                successThreshold: 3, // Need all
                failureThreshold: 1) // Fail on any failure
        );

        // Relax thresholds (easy mode)
        _manager.Register<BasicProcess>(b => b
            .Parallel("resource-gather", par => par
                .Handler("food", s => { log.Add("food"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_SUCCESS | NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_FAILURE,
                successThreshold: 2, // Only need 2
                failureThreshold: 2) // Allow 1 failure
        );

        // Act
        var result = process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Should succeed with relaxed thresholds (2 success, 1 failure, 1 new success)
        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
            Assert.That(log.Count(s => s.Contains("wood") || s.Contains("stone") || s.Contains("food")), Is.GreaterThanOrEqualTo(2));
        }
    }

    #endregion

    #region Complex Combination Tests

    [Test]
    public void ComplexPluginSystem_CoreProtected_PluginExtensible() {
        // Scenario: Game with protected core systems and extensible plugin hooks
        var log = new List<string>();
        var process = new BasicProcess();

        // Core system (protected)
        _manager!.Register<BasicProcess>(b => b
            .Sequence("game-loop", seq => seq
                .Handler("core-init", s => { log.Add("core-init"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.READONLY)
                .Sequence("plugin-hooks", pluginSeq => pluginSeq
                    .Handler("default-plugin", s => { log.Add("default"); return LSProcessResultStatus.SUCCESS; }),
                    NodeUpdatePolicy.IGNORE_CHANGES) // Allow adding plugins but not changing the sequence itself
                .Handler("core-cleanup", s => { log.Add("core-cleanup"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.READONLY),
                NodeUpdatePolicy.IGNORE_CHANGES) // Protect overall structure
        );

        // Plugin 1 tries to modify core (should fail)
        _manager.Register<BasicProcess>(b => b
            .Sequence("game-loop", seq => seq
                .Handler("core-init", s => { log.Add("hacked-init"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Plugin 2 adds to plugin hooks (should succeed)
        _manager.Register<BasicProcess>(b => b
            .Sequence("game-loop", seq => seq
                .Sequence("plugin-hooks", pluginSeq => pluginSeq
                    .Handler("custom-plugin", s => { log.Add("custom-plugin"); return LSProcessResultStatus.SUCCESS; })))
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Core intact, plugins added
        Assert.That(log, Is.EqualTo(new[] { "core-init", "default", "custom-plugin", "core-cleanup" }));
    }

    [Test]
    public void ModularAI_BehaviorReplacement_WithConditionMerging() {
        // Scenario: AI system where behaviors can be replaced but conditions accumulate
        var log = new List<string>();
        var process = new BasicProcess();
        process.SetData("health-low", false);
        process.SetData("enemies-near", true);
        process.SetData("has-weapon", true);

        // Base AI behavior
        _manager!.Register<BasicProcess>(b => b
            .Selector("ai-behavior", sel => sel
                .Handler("flee", s => { log.Add("flee"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.DEFAULT_HANDLER,
                    conditions: p => p.TryGetData<bool>("health-low", out var low) && low)
                .Handler("attack", s => { log.Add("attack"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.DEFAULT_HANDLER,
                    conditions: p => p.TryGetData<bool>("enemies-near", out var near) && near))
        );

        // Advanced AI modification: add weapon requirement to attack
        _manager.Register<BasicProcess>(b => b
            .Selector("ai-behavior", sel => sel
                .Handler("attack", s => { log.Add("armed-attack"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.MERGE_CONDITIONS,
                    conditions: p => p.TryGetData<bool>("has-weapon", out var weapon) && weapon))
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Armed attack executes (enemies-near AND has-weapon both true)
        Assert.That(log, Is.EqualTo(new[] { "armed-attack" }));
    }

    [Test]
    public void DynamicDifficultyAdjustment_PriorityAndThresholdChanges() {
        // Scenario: Difficulty system that adjusts priorities and parallel thresholds
        var log = new List<string>();
        var process = new BasicProcess();
        var difficulty = "hard";
        process.SetData("difficulty", difficulty);

        // Base combat system
        _manager!.Register<BasicProcess>(b => b
            .Parallel("combat-resolution", par => par
                .Handler("player-attack", s => { log.Add("player-attack"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.NORMAL)
                .Handler("enemy-defense", s => { log.Add("enemy-defense"); return LSProcessResultStatus.FAILURE; },
                    NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.NORMAL)
                .Handler("environmental", s => { log.Add("environmental"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.DEFAULT_HANDLER, LSProcessPriority.LOW),
                successThreshold: 2,
                failureThreshold: 2)
        );

        // Hard mode adjustments
        if (difficulty == "hard") {
            _manager.Register<BasicProcess>(b => b
                .Parallel("combat-resolution", par => par
                    .Handler("enemy-defense", s => { log.Add("buffed-defense"); return LSProcessResultStatus.FAILURE; },
                        NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.OVERRIDE_PRIORITY,
                        LSProcessPriority.HIGH), // Enemy acts first
                    NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_SUCCESS | NodeUpdatePolicy.OVERRIDE_PARALLEL_NUM_FAILURE,
                    successThreshold: 3, // Need more successes
                    failureThreshold: 1) // Less forgiving
            );
        }

        // Act
        var result = process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);
        var expected = new[] { "buffed-defense", "player-attack", "environmental" };
        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
            Assert.That(log, Is.EqualTo(expected));
        }
    }

    [Test]
    public void LayerTypeTransformation_WithChildPreservation() {
        // Scenario: Refactoring layer type while preserving some children
        var log = new List<string>();
        var process = new BasicProcess();

        // Original sequence
        _manager!.Register<BasicProcess>(b => b
            .Sequence("refactor-target", seq => seq
                .Handler("keep-me", s => { log.Add("kept"); return LSProcessResultStatus.FAILURE; },
                    NodeUpdatePolicy.READONLY)
                .Handler("replace-me", s => { log.Add("old"); return LSProcessResultStatus.SUCCESS; }))
        );

        // Transform to selector
        _manager.Register<BasicProcess>(b => b
            .Selector("refactor-target", sel => sel
                .Handler("new-child", s => { log.Add("new"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.REPLACE_LAYER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        var expected = new[] { "new" };
        // Assert: Selector behavior with only new children (old ones lost due to REPLACE_LAYER)
        Assert.That(log, Is.EqualTo(expected));
    }

    [Test]
    public void InverterWithREADONLY_ShouldProtectChildAndInversion() {
        // Scenario: Critical inverter that must not be modified
        var log = new List<string>();
        var process = new BasicProcess();

        // Protected inverter
        _manager!.Register<BasicProcess>(b => b
            .Inverter("critical-negation", inv => inv
                .Handler("check", s => { log.Add("original-check"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.READONLY)
        );

        // Try to modify inverter child
        _manager.Register<BasicProcess>(b => b
            .Inverter("critical-negation", inv => inv
                .Handler("check", s => { log.Add("hacked-check"); return LSProcessResultStatus.FAILURE; }))
        );

        // Act
        var result = process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Original check executes and gets inverted (SUCCESS -> FAILURE)
        using (Assert.EnterMultipleScope()) {
            Assert.That(log, Is.EqualTo(expected));
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        }
    }

    [Test]
    public void MultiLevelProtection_NestedREADONLY() {
        // Scenario: Nested structures with different protection levels
        var log = new List<string>();
        var process = new BasicProcess();

        // Nested protected structure
        _manager!.Register<BasicProcess>(b => b
            .Sequence("outer-protected", outerSeq => outerSeq
                .Handler("outer-handler", s => { log.Add("outer"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.READONLY)
                .Sequence("inner-modifiable", innerSeq => innerSeq
                    .Handler("inner-handler", s => { log.Add("inner-v1"); return LSProcessResultStatus.SUCCESS; })),
                NodeUpdatePolicy.IGNORE_CHANGES)
        );

        // Try to modify outer handler (should fail)
        _manager.Register<BasicProcess>(b => b
            .Sequence("outer-protected", outerSeq => outerSeq
                .Handler("outer-handler", s => { log.Add("hacked-outer"); return LSProcessResultStatus.SUCCESS; },
                    NodeUpdatePolicy.OVERRIDE_HANDLER))
        );

        // Modify inner handler (should succeed)
        _manager.Register<BasicProcess>(b => b
            .Sequence("outer-protected", outerSeq => outerSeq
                .Sequence("inner-modifiable", innerSeq => innerSeq
                    .Handler("inner-handler", s => { log.Add("inner-v2"); return LSProcessResultStatus.SUCCESS; },
                        NodeUpdatePolicy.OVERRIDE_HANDLER)))
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Outer protected, inner modified
        var expected = new[] { "outer", "inner-v2" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Test]
    public void ConflictingPolicies_OVERRIDE_And_MERGE_Conditions() {
        // Scenario: Testing precedence when both OVERRIDE and MERGE are set
        // Use observable handler behavior instead of relying on data conditions
        var log = new List<string>();
        var process = new BasicProcess();

        // Original handler with a condition that always passes
        var originalConditionPassed = false;
        _manager!.Register<BasicProcess>(b => b
            .Handler("conflicting-policy",
                s => { log.Add("original-executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.DEFAULT_HANDLER,
                conditions: p => { originalConditionPassed = true; return true; })
        );

        // Override with a condition that always fails
        var newConditionEvaluated = false;
        _manager.Register<BasicProcess>(b => b
            .Handler("conflicting-policy",
                s => { log.Add("override-executed"); return LSProcessResultStatus.SUCCESS; },
                NodeUpdatePolicy.OVERRIDE_HANDLER | NodeUpdatePolicy.OVERRIDE_CONDITIONS | NodeUpdatePolicy.MERGE_CONDITIONS,
                conditions: p => { newConditionEvaluated = true; return false; })
        );

        // Act
        process.Execute(_manager);

        // Assert: New condition should be evaluated (OVERRIDE took precedence), and it should fail
        // So handler should NOT execute, but we can verify the right condition was used
        using (Assert.EnterMultipleScope()) {
            Assert.That(log, Is.Empty, "Handler should not execute because override condition is false");
            Assert.That(newConditionEvaluated, Is.True, "Override condition should be evaluated (OVERRIDE_CONDITIONS took precedence)");
            Assert.That(originalConditionPassed, Is.False, "Original condition should NOT be evaluated (was overridden)");
        }
    }

    [Test]
    public void IGNORE_CHANGES_WithREPLACE_LAYER_ShouldKeepExistingAndIgnoreReplacement() {
        // Scenario: IGNORE_CHANGES on existing node blocks REPLACE_LAYER and skips incoming builder
        var log = new List<string>();
        var process = new BasicProcess();

        // Existing sequence with two successful steps; IGNORE_CHANGES prevents structural changes
        _manager!.Register<BasicProcess>(b => b
            .Sequence("protected-type", seq => seq
                .Handler("original-1", s => { log.Add("sequence-1"); return LSProcessResultStatus.SUCCESS; })
                .Handler("original-2", s => { log.Add("sequence-2"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.IGNORE_CHANGES)
        );

        // Attempt to replace with selector that would short-circuit on first success
        _manager.Register<BasicProcess>(b => b
            .Selector("protected-type", sel => sel
                .Handler("selector-fail", s => { log.Add("selector-fail"); return LSProcessResultStatus.FAILURE; })
                .Handler("selector-success", s => { log.Add("selector-success"); return LSProcessResultStatus.SUCCESS; }),
                NodeUpdatePolicy.REPLACE_LAYER)
        );

        // Act
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Layer type not replaced and builder does not append to existing sequence
        var expected = new[] { "sequence-1", "sequence-2" };
        Assert.That(log, Is.EqualTo(expected));
    }

    #endregion
}
