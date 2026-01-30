using NUnit.Framework;
using LSUtils.ProcessSystem;
using LSUtils.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Tests.ProcessSystem;

/// <summary>
/// Test processable for LSProcessManager tests
/// </summary>
internal class TestProcessable : ILSProcessable {
    public Guid ID { get; } = Guid.NewGuid();
    public bool InitializeCalled { get; private set; }
    public string Name { get; set; } = "Test";

    public LSProcessResultStatus Initialize(LSProcessBuilderAction? onInitialize = null,
        LSProcessManager? manager = null, params ILSProcessable[]? forwardProcessables) {
        InitializeCalled = true;
        return LSProcessResultStatus.SUCCESS;
    }
}

[TestFixture]
public class LSProcessManagerTests {
    private LSProcessManager? _manager;

    [SetUp]
    public void SetUp() {
        _manager = new LSProcessManager();
    }

    [TearDown]
    public void TearDown() {
        _manager = null;
    }

    [Test]
    public void Constructor_ShouldInitializeSuccessfully() {
        // Arrange & Act
        var manager = new LSProcessManager();

        // Assert
        Assert.That(manager, Is.Not.Null);
    }

    [Test]
    public void Singleton_ShouldReturnSameInstance() {
        // Act
        var instance1 = LSProcessManager.Singleton;
        var instance2 = LSProcessManager.Singleton;

        // Assert
        Assert.That(instance1, Is.SameAs(instance2));
        Assert.That(instance1, Is.Not.Null);
    }

    [Test]
    public void DebugLogging_ShouldConfigureLoggingLevels() {
        // Arrange
        var originalLevel = LSLogger.Singleton.MinimumLevel;

        try {
            // Act
            LSProcessManager.DebugLogging(true, LSLogLevel.DEBUG);

            // Assert
            Assert.That(LSLogger.Singleton.MinimumLevel, Is.EqualTo(LSLogLevel.DEBUG));
        } finally {
            // Cleanup
            LSLogger.Singleton.MinimumLevel = originalLevel;
        }
    }

    [Test]
    public void DebugLogging_WithFalse_ShouldDisableLogging() {
        // Act & Assert - Should not throw
        Assert.DoesNotThrow(() => LSProcessManager.DebugLogging(false));
    }

    [Test]
    public void LSProcessContextMode_ShouldHaveExpectedValues() {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.LSProcessContextMode),
            LSProcessManager.LSProcessContextMode.ALL), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.LSProcessContextMode),
            LSProcessManager.LSProcessContextMode.LOCAL), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.LSProcessContextMode),
            LSProcessManager.LSProcessContextMode.MATCH_FIRST), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSProcessManager.LSProcessContextMode),
            LSProcessManager.LSProcessContextMode.GLOBAL), Is.True);
    }

    [Test]
    public void GetRootNode_ShouldReturnValidNode() {
        // Arrange
        var process = new TestProcess();
        var processable = new TestProcessable();
        // Use WithProcessing to create the tree using public API
        process.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        var localRoot = process.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(process) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out var availableInstances, false,
            processable);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithNullLocalRoot_ShouldHandleGracefully() {
        // Arrange
        var processable = new TestProcessable();

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out var availableInstances,
            false,
            processable);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithEmptyInstances_ShouldHandleGracefully() {
        // Arrange
        // var tempProcess = new TestProcess(); //this is not needed in this context, GetRootNode does not use local contexts anymore;
        // tempProcess.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out var availableInstances);
        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithNullInstances_ShouldHandleGracefully() {
        // Arrange
        // var tempProcess = new TestProcess();
        // tempProcess.WithProcessing(builder => builder.Handler("test", session => LSProcessResultStatus.SUCCESS));
        // var localRoot = tempProcess.GetType().GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(tempProcess) as ILSProcessLayerNode;

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out var availableInstances,
            false,
            null);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
    }

    [Test]
    public void GetRootNode_WithFirstBehaviour_ShouldLimitInstances() {
        // Arrange
        var processable1 = new TestProcessable();
        var processable2 = new TestProcessable();

        var executed_nodes = new List<string>();

        // register context for processables
        _manager!.Register<TestProcess>(root => root
            .Handler("test-handler", session => LSProcessResultStatus.SUCCESS) // we aren't actually executing anything
        , processable1);
        _manager!.Register<TestProcess>(root => root
            .Handler("test-handler", session => LSProcessResultStatus.SUCCESS) // we aren't actually executing anything
        , processable2);

        // Act
        var rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out var availableInstances,
            true,
            processable1, processable2);

        // Assert
        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
        Assert.That(availableInstances.Length, Is.EqualTo(1));
        Assert.That(availableInstances[0].ID, Is.EqualTo(processable1.ID));

        rootNode = _manager!.GetRootNode(
            typeof(TestProcess),
            out availableInstances,
            true,
            processable2, processable1);

        Assert.That(rootNode, Is.Not.Null);
        Assert.That(availableInstances, Is.Not.Null);
        Assert.That(availableInstances.Length, Is.EqualTo(1));
        Assert.That(availableInstances[0].ID, Is.EqualTo(processable2.ID));
    }

    [Test]
    public void ProcessWithManager_ShouldExecuteSuccessfully() {
        // Arrange
        var process = new TestProcess();

        // Act
        var result = process.Execute(_manager!, LSProcessManager.LSProcessContextMode.ALL);

        // Assert
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsExecuted, Is.True);
    }

    [Test]
    public void MultipleProcesses_ShouldExecuteIndependently() {
        // Arrange
        var process1 = new TestProcess();
        var process2 = new TestProcess();

        // Act
        var result1 = process1.Execute(_manager!, LSProcessManager.LSProcessContextMode.ALL);
        var result2 = process2.Execute(_manager!, LSProcessManager.LSProcessContextMode.ALL);

        // Assert
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process1.IsExecuted, Is.True);
        Assert.That(process2.IsExecuted, Is.True);
        Assert.That(process1.ID, Is.Not.EqualTo(process2.ID));
    }

    [Test]
    public void RegisterGlobal_ShouldApplyToAllInstances() {
        // Arrange
        var global_executed = new List<string>();
        var processable1 = new TestProcessable { Name = "Entity1" };
        var processable2 = new TestProcessable { Name = "Entity2" };

        _manager!.Register<TestProcess>(root => root
            .Handler("global-handler", session => {
                // add the (session.Instances) identifiers to the list 
                foreach (var inst in session.Instances ?? Array.Empty<ILSProcessable>()) {
                    if (inst is TestProcessable tp) {
                        global_executed.Add(tp.Name);
                    }
                }
                //global_executed.Add(session.SessionID.ToString());
                return LSProcessResultStatus.SUCCESS;
            })
        );

        // Act: Execute two processes with different instances
        var process1 = new TestProcess();
        var result1 = process1.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, processable1);

        var process2 = new TestProcess();
        var result2 = process2.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, processable2);

        // Assert: Global handler should execute for both
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(global_executed, Contains.Item("Entity1"));
        Assert.That(global_executed, Contains.Item("Entity2"));
    }

    [Test]
    public void RegisterInstance_ShouldApplyOnlyToTarget() {
        // Arrange
        var instance_executed = new List<string>();
        var targetEntity = new TestProcessable { Name = "VIP" };
        var otherEntity = new TestProcessable { Name = "Regular" };

        _manager!.Register<TestProcess>(root => root
            .Handler("instance-handler", session => {
                foreach (var inst in session.Instances ?? Array.Empty<ILSProcessable>()) {
                    if (inst is TestProcessable tp) {
                        instance_executed.Add(tp.Name);
                    }
                }
                return LSProcessResultStatus.SUCCESS;
            })
        , targetEntity);

        // Act: Execute with target entity
        var process1 = new TestProcess();
        process1.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, targetEntity);
        Assert.That(instance_executed, Contains.Item("VIP"));


        instance_executed.Clear(); // Reset flag

        // Execute with different entity - should NOT use instance context
        var process2 = new TestProcess();
        process2.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, otherEntity);

        // Assert: Instance handler should only execute for target entity
        Assert.That(instance_executed, Does.Not.Contain("Regular"));
    }

    [Test]
    public void ContextMerge_DecoratorsShouldCombineChildren() {
        // Arrange
        var log = new List<string>();

        _manager!.Register<TestProcess>(root => root
            .Sequence("test-seq", seq => seq
                .Handler("global-h1", s => { log.Add("global-h1"); return LSProcessResultStatus.SUCCESS; })
                .Handler("global-h2", s => { log.Add("global-h2"); return LSProcessResultStatus.SUCCESS; })
            )
        );

        var entity = new TestProcessable();
        _manager.Register<TestProcess>(root => root
            .Sequence("test-seq", seq => seq  // Same ID - decorator MERGES
                .Handler("instance-h3", s => { log.Add("instance-h3"); return LSProcessResultStatus.SUCCESS; })
            )
        , entity);

        // Act
        var process = new TestProcess();
        process.WithProcessing(b => b
            .Sequence("test-seq", seq => seq  // Same ID - decorator MERGES
                .Handler("local-h4", s => { log.Add("local-h4"); return LSProcessResultStatus.SUCCESS; })
            )
        );

        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, entity);

        // Assert: All children should be merged
        Assert.That(log, Contains.Item("global-h1"));
        Assert.That(log, Contains.Item("global-h2"));
        Assert.That(log, Contains.Item("instance-h3"));
        Assert.That(log, Contains.Item("local-h4"));
    }

    [Test]
    public void ContextMerge_HandlersShouldOverride() {
        // Arrange
        var log = new List<string>();

        _manager!.Register<TestProcess>(root => root
            .Handler("override-handler", s => { log.Add("global"); return LSProcessResultStatus.SUCCESS; })
        );

        var entity = new TestProcessable();
        _manager.Register<TestProcess>(root => root
            .Handler("override-handler", s => { log.Add("instance"); return LSProcessResultStatus.SUCCESS; })  // Same ID - OVERRIDE
        , entity);

        // Act
        var process = new TestProcess();
        process.WithProcessing(b => b
            .Handler("override-handler", s => { log.Add("local"); return LSProcessResultStatus.SUCCESS; })  // Same ID - OVERRIDE
        );

        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, entity);

        // Assert: Only highest-priority (local) should execute
        Assert.That(log, Contains.Item("local"));
        Assert.That(log, Does.Not.Contain("global"));
        Assert.That(log, Does.Not.Contain("instance"));
        Assert.That(log.Count, Is.EqualTo(1));
    }

    [Test]
    public void LSProcessContextMode_GLOBAL_ShouldIncludeGlobal() {
        // Arrange
        var log = new List<string>();
        var entity = new TestProcessable();

        _manager!.Register<TestProcess>(root => root
            .Handler("global-h", s => { log.Add("global"); return LSProcessResultStatus.SUCCESS; })
        );

        _manager.Register<TestProcess>(root => root
            .Handler("instance-h", s => { log.Add("instance"); return LSProcessResultStatus.SUCCESS; })
        , entity);

        // Act
        var process = new TestProcess();
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.GLOBAL, entity);

        // Assert: Should include global handler but exclude instance handler
        Assert.That(log, Contains.Item("global"));
        Assert.That(log, Does.Not.Contain("instance"));
    }

    [Test]
    public void LSProcessContextMode_MATCH_FIRST_ShouldFindFirst() {
        // Arrange
        var entity1 = new TestProcessable { Name = "E1" };
        var entity2 = new TestProcessable { Name = "E2" };
        var entity3 = new TestProcessable { Name = "E3" };

        _manager!.Register<TestProcess>(root => root
            .Handler("e2-handler", s => LSProcessResultStatus.SUCCESS)
        , entity2);

        // Act: Try to match in order E1, E2, E3
        var root = _manager.GetRootNode(typeof(TestProcess), out var available,
            true, entity1, entity2, entity3);

        // Assert: Should find and return only entity2
        Assert.That(available!.Length, Is.EqualTo(1));
        Assert.That(available[0].ID, Is.EqualTo(entity2.ID));
    }

    [Test]
    public void LSProcessContextMode_ALL_INSTANCES_ShouldFindAll() {
        // Arrange
        var entity1 = new TestProcessable { Name = "E1" };
        var entity2 = new TestProcessable { Name = "E2" };
        var entity3 = new TestProcessable { Name = "E3" };

        _manager!.Register<TestProcess>(root => root
            .Handler("e1-handler", s => LSProcessResultStatus.SUCCESS)
        , entity1);

        _manager.Register<TestProcess>(root => root
            .Handler("e3-handler", s => LSProcessResultStatus.SUCCESS)
        , entity3);

        // Act: Try to match all in E1, E2, E3 with ALL_INSTANCES behavior
        var root = _manager.GetRootNode(typeof(TestProcess), out var available,
            false, entity1, entity2, entity3);

        // Assert: Should find all instances that have contexts registered (entity1, entity3)
        Assert.That(available!.Length, Is.GreaterThanOrEqualTo(1)); // At least entity1 matched
        Assert.That(available, Does.Contain(entity1));
        Assert.That(available, Does.Not.Contain(entity2));
        Assert.That(available, Does.Contain(entity3));
    }

    [Test]
    public void MultipleRegistrations_ShouldAppendHandlers() {
        // Arrange
        var log = new List<string>();

        // First registration
        _manager!.Register<TestProcess>(root => root
            .Handler("h1", s => { log.Add("h1"); return LSProcessResultStatus.SUCCESS; })
        );

        // Second registration (same type, no instance) - should merge
        _manager.Register<TestProcess>(root => root
            .Handler("h2", s => { log.Add("h2"); return LSProcessResultStatus.SUCCESS; })
        );

        // Act
        var process = new TestProcess();
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Both should execute
        Assert.That(log, Contains.Item("h1"));
        Assert.That(log, Contains.Item("h2"));
    }

    [Test]
    public void ContextCloning_ShouldPreserveOriginalRegistration() {
        // Arrange
        var execution_count = 0;

        _manager!.Register<TestProcess>(root => root
            .Handler("count", s => { execution_count++; return LSProcessResultStatus.SUCCESS; })
        );

        // Act: Execute multiple times
        var process1 = new TestProcess();
        process1.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        var process2 = new TestProcess();
        process2.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        var process3 = new TestProcess();
        process3.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Handler should have been invoked 3 times from fresh contexts
        Assert.That(execution_count, Is.EqualTo(3));
    }

    [Test]
    public void NodeManipulation_WithProcessing_ShouldAddNewHandlers() {
        // Arrange
        var log = new List<string>();

        _manager!.Register<TestProcess>(root => root
            .Handler("built-in", s => { log.Add("built-in"); return LSProcessResultStatus.SUCCESS; })
        );

        // Act
        var process = new TestProcess();
        process.WithProcessing(b => b
            .Handler("added-runtime", s => { log.Add("added-runtime"); return LSProcessResultStatus.SUCCESS; })
        );

        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL);

        // Assert: Both built-in and runtime-added handler should execute
        Assert.That(log, Contains.Item("built-in"));
        Assert.That(log, Contains.Item("added-runtime"));
    }

    [Test]
    public void ReadOnly_ShouldPreventOverride() {
        // Arrange
        var log = new List<string>();

        _manager!.Register<TestProcess>(root => root
            .Handler("protected", s => { log.Add("original"); return LSProcessResultStatus.SUCCESS; }, updatePolicy: NodeUpdatePolicy.IGNORE_CHANGES)
        );

        // Act: Try to override with instance context
        var entity = new TestProcessable();
        _manager.Register<TestProcess>(root => root
            .Handler("protected", s => { log.Add("override"); return LSProcessResultStatus.SUCCESS; })  // Will be ignored due to readonly
        , entity);

        var process = new TestProcess();
        process.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, entity);

        // Assert: Original readonly handler should execute, not override
        Assert.That(log, Contains.Item("original"));
        Assert.That(log, Does.Not.Contain("override"));
    }

    [Test]
    public void ComplexScenario_GlobalModifiersWithInstanceCustomization() {
        // Arrange
        var vipLog = new List<string>();
        var vip = new TestProcessable { Name = "VIP" };

        // Global: all modifiers
        _manager!.Register<TestProcess>(root => root
            .Sequence("modifiers", seq => seq
                .Handler("mod-1", s => { vipLog.Add("global-mod-1"); return LSProcessResultStatus.SUCCESS; })
                .Handler("mod-2", s => { vipLog.Add("global-mod-2"); return LSProcessResultStatus.SUCCESS; })
            )
        );

        // VIP: override mod-2 with bonus
        _manager.Register<TestProcess>(root => root
            .Sequence("modifiers", seq => seq
                .Handler("mod-2", s => { vipLog.Add("vip-bonus"); return LSProcessResultStatus.SUCCESS; })  // Overrides global mod-2
            )
        , vip);

        // Act: Execute for VIP
        var vipProcess = new TestProcess();
        vipProcess.Execute(_manager, LSProcessManager.LSProcessContextMode.ALL, vip);

        // Assert VIP execution
        // VIP: should have global-mod-1 + vip-bonus (mod-2 overridden)
        Assert.That(vipLog, Contains.Item("global-mod-1"));
        Assert.That(vipLog, Contains.Item("vip-bonus"));
        // Should not have original global-mod-2 since it was overridden
        var countGlobalMod2 = vipLog.Where(x => x == "global-mod-2").Count();
        Assert.That(countGlobalMod2, Is.EqualTo(0));
    }

    [Test]
    public void Concurrent_MultipleRegistrations_ShouldBeSafe() {
        // Arrange
        var tasks = new List<System.Threading.Tasks.Task>();

        // Act: Register concurrently from multiple tasks
        for (int i = 0; i < 10; i++) {
            int idx = i;
            tasks.Add(System.Threading.Tasks.Task.Run(() => {
                var entity = new TestProcessable { Name = $"Entity{idx}" };
                _manager!.Register<TestProcess>(root => root
                    .Handler($"h{idx}", s => LSProcessResultStatus.SUCCESS)
                , entity);
            }));
        }

        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

        // Assert: All registrations should succeed without exceptions
        Assert.That(tasks.TrueForAll(t => t.IsCompletedSuccessfully), Is.True);
    }

    [Test]
    public void CreateRootNode_ShouldCreateSequenceRoot() {
        // Act
        var root = LSProcessManager.CreateRootNode("test-root");

        // Assert
        Assert.That(root, Is.Not.Null);
        Assert.That(root.NodeID, Is.EqualTo("test-root"));
        Assert.That(root, Is.InstanceOf<LSProcessNodeSequence>());
    }
}
