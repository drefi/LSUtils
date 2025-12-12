namespace LSUtils.ProcessSystem.Tests;

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Logging;
/// <summary>
/// Tests for context manager functionality and registration.
/// </summary>
[TestFixture]
public class LSProcessManager_Tests {
    public class MockProcess : LSProcess {
        public MockProcess() { }
    }

    private LSProcessHandler _mockHandler1;
    private LSProcessHandler _mockHandler2;
    private LSProcessHandler _mockHandler3Failure;
    private LSProcessHandler _mockHandler3Cancel;
    private LSProcessHandler _mockHandler3Waiting;
    private int _handler1CallCount;
    private int _handler2CallCount;
    private int _handler3CallCount;
    private LSLogger _logger;

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

    [TearDown]
    public void Cleanup() {
        // Reset for next test
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;
    }

    [Test]
    public void LSProcessManagerRegistrationAndRetrieval() {
        LSProcessManager.Singleton.Register<MockProcess>(root => root
            .Sequence("seq", seq => seq
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2))
        , null);
        LSProcessManager.Singleton.Register<MockProcess>(root => root
            .Selector("sel", sel => sel
                .Handler("handler3", _mockHandler1)
                .Handler("handler4", _mockHandler2))
        , null);

        var root = LSProcessManager.Singleton.GetRootNode<MockProcess>();
        var typeName = typeof(MockProcess).Name;
        Assert.That(root.NodeID, Is.EqualTo(typeName));
        Assert.That(root, Is.InstanceOf<LSProcessNodeParallel>());

        Assert.That(root.HasChild("seq"), Is.True);
        Assert.That(root.HasChild("sel"), Is.True);

        var mockProcess = new MockProcess();
        var result = mockProcess.WithProcessing(s => s
            .Sequence("seq", seq => seq
                .Handler("test", session => {
                    LSLogger.Singleton.Info("Executing handler in test process.", source: (typeName, true));
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("handler5", _mockHandler1)
                .Handler("handler6", _mockHandler2))
        , LSProcessLayerNodeType.PARALLEL).Execute();

        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));
        Assert.That(_handler1CallCount, Is.EqualTo(3)); // handler1, handler3 and handler5
        Assert.That(_handler2CallCount, Is.EqualTo(2)); // handler2 and handler6
    }
}
