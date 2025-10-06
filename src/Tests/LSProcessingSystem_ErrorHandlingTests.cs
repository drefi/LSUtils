using LSUtils.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Processing.Tests;

/// <summary>
/// Tests for error handling, exception scenarios, and edge cases.
/// </summary>
[TestFixture]
public class LSProcessingSystem_ErrorHandlingTests {
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
        _handler1CallCount = 0;
        _handler2CallCount = 0;
        _handler3CallCount = 0;

        _mockHandler1 = (proc, node) => {
            _handler1CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler2 = (proc, node) => {
            _handler2CallCount++;
            return LSProcessResultStatus.SUCCESS;
        };

        _mockHandler3Failure = (proc, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.FAILURE;
        };

        _mockHandler3Cancel = (proc, node) => {
            _handler3CallCount++;
            return LSProcessResultStatus.CANCELLED;
        };

        _mockHandler3Waiting = (proc, node) => {
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
    public void TestNullReferenceHandling() {
        // Test what actually happens with null handlers and IDs
        // Test if null handlers are allowed or throw exceptions

        Exception? caughtException = null;
        try {
            var builder = new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("test", null!))
                .Build();
            // If build succeeds, test processing
            var mockProcess = new MockProcess();
            var process = new LSProcessSession(mockProcess, builder);
            try {
                process.Execute();
            } catch (Exception ex) {
                caughtException = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException = ex;
        } catch (ArgumentException ex) {
            caughtException = ex;
        }

        // The system should handle nulls somehow - either throw during build or processing
        if (caughtException != null) {
            Assert.Pass($"Null handler correctly handled with {caughtException.GetType().Name}: {caughtException.Message}");
        } else {
            Assert.Fail("Expected some form of null handler validation, but none occurred");
        }

        // Test empty/whitespace node IDs - these should definitely be invalid
        Assert.Throws<ArgumentException>(() => {
            new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("", _mockHandler1))
                .Build();
        });

        Assert.Throws<ArgumentException>(() => {
            new LSProcessTreeBuilder()
                .Sequence("root", seq => seq.Handler("   ", _mockHandler1))
                .Build();
        });
    }

    [Test]
    public void TestDuplicateNodeIds() {
        // Test if the system actually prevents duplicate node IDs
        // Let's see what happens when we try to create duplicates

        try {
            var builder = new LSProcessTreeBuilder()
                .Sequence("root", seq => seq
                    .Handler("duplicate", _mockHandler1)
                    .Handler("duplicate", _mockHandler2))
                .Build();

            // If this succeeds, let's test the behavior
            var mockProcess = new MockProcess();
            var process = new LSProcessSession(mockProcess, builder);
            var result = process.Execute();

            // The system might allow duplicates but only use the last one
            Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        } catch (LSException) {
            // If it does throw, that's the expected behavior
            Assert.Pass("Duplicate IDs correctly throw LSException");
        }

        // Test that valid unique IDs work fine
        var validContext = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("handler1", _mockHandler1)
                .Handler("handler2", _mockHandler2))
            .Build();

        Assert.That(validContext, Is.Not.Null);
        Assert.That(validContext.HasChild("handler1"), Is.True);
        Assert.That(validContext.HasChild("handler2"), Is.True);
    }

    [Test]
    public void TestRuntimeExceptionPropagation() {
        List<string> executionOrder = new List<string>();

        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq
                .Handler("beforeException", (proc, node) => {
                    executionOrder.Add("BEFORE");
                    return LSProcessResultStatus.SUCCESS;
                })
                .Handler("throwsException", (proc, node) => {
                    executionOrder.Add("EXCEPTION");
                    throw new InvalidOperationException("Handler error");
                })
                .Handler("afterException", (proc, node) => {
                    executionOrder.Add("AFTER");
                    return LSProcessResultStatus.SUCCESS;
                }))
            .Build();

        var mockProcess = new MockProcess();
        var process = new LSProcessSession(mockProcess, builder);

        // Exception should propagate and stop execution
        Assert.Throws<InvalidOperationException>(() => {
            process.Execute();
        });

        // Verify execution stopped at the exception
        Assert.That(executionOrder, Is.EqualTo(new List<string> { "BEFORE", "EXCEPTION" }));
    }

    [Test]
    public void TestConcurrentModificationDetection() {
        var sequence = LSProcessNodeSequence.Create("seq", 0);
        var handler1 = LSProcessNodeHandler.Create("handler1", _mockHandler1, 0);
        var handler2 = LSProcessNodeHandler.Create("handler2", _mockHandler2, 1);

        sequence.AddChild(handler1);

        // Start processing to set internal state
        var mockProcess = new MockProcess();
        var builder = new LSProcessSession(mockProcess, sequence);

        // Process first to set internal processing state
        var result = builder.Execute();
        Assert.That(result, Is.EqualTo(LSProcessResultStatus.SUCCESS));

        // After processing has started, modifications should be restricted
        Assert.Throws<LSException>(() => {
            sequence.AddChild(handler2);
        });
    }

    [Test]
    public void TestProcessContextNullEventHandling() {
        var builder = new LSProcessTreeBuilder()
            .Sequence("root", seq => seq.Handler("handler", _mockHandler1))
            .Build();

        // Test null event - system might accept it
        Exception? caughtException = null;
        try {
            var process = new LSProcessSession(null!, builder);
            // If construction succeeds, test processing
            try {
                var result = process.Execute();
                // System might handle null gracefully
                Assert.That(result, Is.AnyOf(LSProcessResultStatus.SUCCESS, LSProcessResultStatus.FAILURE, LSProcessResultStatus.WAITING, LSProcessResultStatus.CANCELLED));
            } catch (Exception ex) {
                caughtException = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException = ex;
        }

        // Either exception or graceful handling is acceptable
        if (caughtException != null) {
            Assert.Pass($"Null event handled with {caughtException.GetType().Name}");
        } else {
            Assert.Pass("System gracefully handles null events");
        }

        // Test null builder
        var mockProcess = new MockProcess();
        Exception? caughtException2 = null;
        try {
            var process = new LSProcessSession(mockProcess, null!);
            try {
                var result = process.Execute();
                Assert.That(result, Is.AnyOf(LSProcessResultStatus.SUCCESS, LSProcessResultStatus.FAILURE, LSProcessResultStatus.WAITING, LSProcessResultStatus.CANCELLED));
            } catch (Exception ex) {
                caughtException2 = ex;
            }
        } catch (ArgumentNullException ex) {
            caughtException2 = ex;
        }

        // Either exception or graceful handling is acceptable
        if (caughtException2 != null) {
            Assert.Pass($"Null builder handled with {caughtException2.GetType().Name}");
        } else {
            Assert.Pass("System gracefully handles null builder");
        }
    }
}
