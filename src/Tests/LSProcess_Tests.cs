namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;
[TestFixture]
public class LSProcess_Tests {
    public class MockProcess : LSProcess {
        public MockProcess() { }
    }
    //mock process with processing override
    public class ProcessingMockProcess : LSProcess {
        protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) {
            return builder.Handler("myHandler", session => {
                LSLogger.Singleton.Info("processing is working", source: (nameof(ProcessingMockProcess), true));
                return LSProcessResultStatus.SUCCESS;
            });
        }
    }

    private LSProcessHandler _mockHandler1 = null!;
    private LSProcessHandler _mockHandler2 = null!;
    private LSProcessHandler _mockHandler3Failure = null!;
    private LSProcessHandler _mockHandler3Cancel = null!;
    private LSProcessHandler _mockHandler3Waiting = null!;
    private int _handler1CallCount;
    private int _handler2CallCount;
    private int _handler3CallCount;
    private LSLogger _logger = null!;

    [SetUp]
    public void Setup() {
        _logger = LSLogger.Singleton;
        _logger.ClearProviders();
        _logger.AddProvider(new LSConsoleLogProvider());
        LSProcessManager.DebugLogging();
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;

        _mockHandler1 = (session) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler2 = (session) => {
            _handler2CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler3Failure = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _mockHandler3Cancel = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _mockHandler3Waiting = (session) => {
            _handler3CallCount++;
            return LSProcessResultStatus.WAITING;
        };
    }

    [Test]
    public void TestProcessCreation() {
        var mockProcess = new MockProcess();
        Assert.That(mockProcess, Is.Not.Null);
        Assert.That(mockProcess.ID, Is.Not.EqualTo(System.Guid.Empty));
    }

    [Test]
    public void TestProcessExecute_SingleExecution() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Handler("handler1", _mockHandler1));

        var result1 = mockProcess.Execute();
        var result2 = mockProcess.Execute(); // Second call should return cached result

        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Should only execute once
        Assert.That(mockProcess.IsExecuted, Is.True);
        Assert.That(mockProcess.IsCompleted, Is.True);
    }

    [Test]
    public void TestProcessExecute_SequenceSuccess() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("handler1", _mockHandler1)
                    .Handler("handler2", _mockHandler2)));

        var result = mockProcess.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(1));
        Assert.That(mockProcess.IsCompleted, Is.True);
        Assert.That(mockProcess.IsCancelled, Is.False);
    }

    [Test]
    public void TestProcessExecute_SequenceFailure() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("handler1", _mockHandler1)
                    .Handler("handler3", _mockHandler3Failure)
                    .Handler("handler2", _mockHandler2))); // Should not execute

        var result = mockProcess.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Should not execute after failure
        Assert.That(mockProcess.IsCompleted, Is.True);
    }

    [Test]
    public void TestProcessExecute_WithWaitingStatus() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("handler1", _mockHandler1)
                    .Handler("waitingHandler", _mockHandler3Waiting)
                    .Handler("handler2", _mockHandler2))); // Should not execute initially

        var result = mockProcess.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(_handler1CallCount, Is.EqualTo(1));
        Assert.That(_handler3CallCount, Is.EqualTo(1));
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Should not execute yet
        Assert.That(mockProcess.IsCompleted, Is.False);
        Assert.That(mockProcess.IsCancelled, Is.False);
    }

    [Test]
    public void TestProcessResume_WaitingResume() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Handler("waitingHandler", _mockHandler3Waiting));

        // Execute to get into WAITING state
        var result1 = mockProcess.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.WAITING));

        var result2 = mockProcess.Resume();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestProcessResume_AllWaitingNodes() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Parallel("main", par => par
                    .Handler("handler1", _mockHandler1)
                    .Handler("waitingHandler1", _mockHandler3Waiting)
                    .Handler("waitingHandler2", _mockHandler3Waiting), 1, 1)); // Need 1 success, allow 1 failure

        var result1 = mockProcess.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // Should succeed with handler1

        // Resume all waiting nodes (no specific node IDs)
        var result2 = mockProcess.Resume();
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.SUCCESS)); // Should remain successful
    }

    [Test]
    public void TestProcessFail_SpecificNode() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("handler1", _mockHandler1)
                    .Handler("waitingHandler", _mockHandler3Waiting)
                    .Handler("handler2", _mockHandler2)));

        // Execute until waiting
        var result1 = mockProcess.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.WAITING));

        // Force failure of the waiting handler
        var result2 = mockProcess.Fail("waitingHandler");
        Assert.That(result2, Is.EqualTo(LSProcessResultStatus.FAILURE));
        Assert.That(mockProcess.IsCompleted, Is.True);
    }

    [Test]
    public void TestProcessCancel() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("handler1", _mockHandler1)
                    .Handler("waitingHandler", _mockHandler3Waiting)
                    .Handler("handler2", _mockHandler2)));

        // Execute until waiting
        var result1 = mockProcess.Execute();
        Assert.That(result1, Is.EqualTo(LSProcessResultStatus.WAITING));
        Assert.That(mockProcess.IsCancelled, Is.False);

        // Cancel the process
        mockProcess.Cancel();
        Assert.That(mockProcess.IsCancelled, Is.True);
        Assert.That(mockProcess.IsCompleted, Is.True);
    }

    [Test]
    public void TestProcessDataExchange() {
        LSProcessHandler dataStoreHandler = (session) => {
            session.Process.SetData("testKey", "testValue");
            session.Process.SetData("numberKey", 42);
            return LSProcessResultStatus.SUCCESS;
        };

        LSProcessHandler dataRetrieveHandler = (session) => {
            var stringValue = session.Process.GetData<string>("testKey");
            var numberValue = session.Process.GetData<int>("numberKey");

            Assert.That(stringValue, Is.EqualTo("testValue"));
            Assert.That(numberValue, Is.EqualTo(42));

            // Test TryGetData
            var hasValue = session.Process.TryGetData<string>("testKey", out var retrievedValue);
            Assert.That(hasValue, Is.True);
            Assert.That(retrievedValue, Is.EqualTo("testValue"));

            return LSProcessResultStatus.SUCCESS;
        };

        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Sequence("main", seq => seq
                    .Handler("storeData", dataStoreHandler)
                    .Handler("retrieveData", dataRetrieveHandler)));

        var result = mockProcess.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestProcessTryGetData_NonExistentKey() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Handler("testHandler", session => {
                    var hasValue = session.Process.TryGetData<string>("nonExistentKey", out var value);
                    Assert.That(hasValue, Is.False);
                    Assert.That(value, Is.Null);
                    return LSProcessResultStatus.SUCCESS;
                }));

        var result = mockProcess.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestProcessGetData_ThrowsOnMissingKey() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Handler("testHandler", session => {
                    Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => {
                        session.Process.GetData<string>("nonExistentKey");
                    });
                    return LSProcessResultStatus.SUCCESS;
                }));

        var result = mockProcess.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
    }

    [Test]
    public void TestProcessWithProcessingOverride() {
        var process = new ProcessingMockProcess();
        var result = process.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(process.IsCompleted, Is.True);
    }

    [Test]
    public void TestProcessExecute_ThrowsOnNullManager() {
        var mockProcess = new MockProcess();
        Assert.Throws<LSException>(() => {
            mockProcess.Execute(null!, LSProcessManager.ProcessInstanceBehaviour.ALL);
        });
    }

    [Test]
    public void TestProcessResume_ThrowsWhenNotExecuted() {
        var mockProcess = new MockProcess();
        Assert.Throws<LSException>(() => {
            mockProcess.Resume();
        });
    }

    [Test]
    public void TestProcessFail_ThrowsWhenNotExecuted() {
        var mockProcess = new MockProcess();
        Assert.Throws<LSException>(() => {
            mockProcess.Fail();
        });
    }

    [Test]
    public void TestProcessCancel_ThrowsWhenNotExecuted() {
        var mockProcess = new MockProcess();
        Assert.Throws<LSException>(() => {
            mockProcess.Cancel();
        });
    }

    [Test]
    public void TestProcessSelector_FirstSuccessWins() {
        var mockProcess = new MockProcess()
            .WithProcessing(builder => builder
                .Selector("main", sel => sel
                    .Handler("handler3", _mockHandler3Failure) // This fails
                    .Handler("handler1", _mockHandler1)        // This succeeds
                    .Handler("handler2", _mockHandler2)));     // This should not execute

        var result = mockProcess.Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler3CallCount, Is.EqualTo(1)); // First handler called and failed
        Assert.That(_handler1CallCount, Is.EqualTo(1)); // Second handler called and succeeded
        Assert.That(_handler2CallCount, Is.EqualTo(0)); // Third handler not called (selector stops at first success)
    }

    [Test]
    public void TestProcessCreatedAtAndId() {
        var beforeCreation = System.DateTime.UtcNow;
        var mockProcess = new MockProcess();
        var afterCreation = System.DateTime.UtcNow;

        Assert.That(mockProcess.ID, Is.Not.EqualTo(System.Guid.Empty));
        Assert.That(mockProcess.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreation));
        Assert.That(mockProcess.CreatedAt, Is.LessThanOrEqualTo(afterCreation));

        // Each process should have unique ID
        var anotherProcess = new MockProcess();
        Assert.That(anotherProcess.ID, Is.Not.EqualTo(mockProcess.ID));
    }

}
