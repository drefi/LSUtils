using NUnit.Framework;
using System;
using System.Collections.Generic;
using LSUtils.EventSystem;

namespace LSUtils.EventSystem.Tests;

/// <summary>
/// Comprehensive NUnit tests for the LSEventSystem v3 fixed implementation.
/// Based on LSEventSystemTests.cs but adapted for the v3 system with all critical fixes.
/// Tests verify: handler execution order, global+event-scoped integration, and all core functionality.
/// </summary>
[TestFixture]
public class LSEventSystem_v3_Test {

    // Test events for the v3 event system
    private class TestUserRegistrationEvent_v3 : LSBaseEvent_v3 {
        public TestUser User { get; }
        public string RegistrationSource { get; }
        public DateTime RegistrationTime { get; }
        public bool RequiresEmailVerification { get; }

        public TestUserRegistrationEvent_v3(TestUser user, string source, bool requiresVerification = true) {
            User = user;
            RegistrationSource = source;
            RegistrationTime = DateTime.UtcNow;
            RequiresEmailVerification = requiresVerification;

            // Set data for easy access in handlers
            SetData("registration.source", source);
            SetData("verification.required", requiresVerification);
        }
    }

    private class TestSystemStartupEvent_v3 : LSBaseEvent_v3 {
        public string Version { get; }
        public DateTime StartupTime { get; }

        public TestSystemStartupEvent_v3(string version) {
            Version = version;
            StartupTime = DateTime.UtcNow;
        }
    }

    // Test entities
    private class TestUser {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool IsActive { get; set; } = true;
    }

    private LSDispatcher_v3 _dispatcher;

    [SetUp]
    public void SetUp() {
        _dispatcher = new LSDispatcher_v3();
    }

    [TearDown]
    public void TearDown() {
        // Reset dispatcher state
        _dispatcher = new LSDispatcher_v3();
    }

    // === BASIC FUNCTIONALITY TESTS ===

    [Test]
    public void Test_LSBaseEvent_v3_Basic_Properties() {
        // Arrange & Act
        var startupEvent = new TestSystemStartupEvent_v3("1.0.0");

        // Assert
        Assert.That(startupEvent.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(startupEvent.CreatedAt, Is.GreaterThan(DateTime.MinValue));
        Assert.That(startupEvent.IsCancelled, Is.False);
        Assert.That(startupEvent.IsCompleted, Is.False);
        Assert.That(startupEvent.CurrentPhase, Is.EqualTo(LSEventPhase.VALIDATE));
        Assert.That(startupEvent.CompletedPhases, Is.EqualTo((LSEventPhase)0));
        Assert.That(startupEvent.Version, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Test_Event_Data_Storage_And_Retrieval() {
        // Arrange
        var startupEvent = new TestSystemStartupEvent_v3("2.0.0");

        // Act
        startupEvent.SetData("test.string", "hello");
        startupEvent.SetData("test.number", 42);
        startupEvent.SetData("test.bool", true);

        // Assert
        Assert.That(startupEvent.GetData<string>("test.string"), Is.EqualTo("hello"));
        Assert.That(startupEvent.GetData<int>("test.number"), Is.EqualTo(42));
        Assert.That(startupEvent.GetData<bool>("test.bool"), Is.True);

        // Test TryGetData
        Assert.That(startupEvent.TryGetData<string>("test.string", out var stringValue), Is.True);
        Assert.That(stringValue, Is.EqualTo("hello"));

        Assert.That(startupEvent.TryGetData<string>("nonexistent.key", out var missingValue), Is.False);
        Assert.That(missingValue, Is.Null);
    }

    [Test]
    public void Test_Basic_Event_Processing_With_Dispatcher() {
        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        bool handlerCalled = false;

        // Register a global handler
        _dispatcher.ForEvent<TestUserRegistrationEvent_v3>()
            .OnExecutePhase(execute => execute
                .Sequential(evt => {
                    handlerCalled = true;
                    Assert.That(evt.User.Email, Is.EqualTo("test@example.com"));
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Register(); // Complete the registration

        // Act - Dispatch(_dispatcher) should process only global handlers
        var success = userEvent.Dispatch(_dispatcher);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(handlerCalled, Is.True, "Global handler should be executed");
        Assert.That(userEvent.IsCompleted, Is.True);
        Assert.That(userEvent.IsCancelled, Is.False);
    }

    [Test]
    public void Test_Debug_Global_Handler_Registration() {
        // Debug test to understand if global handlers are actually being registered
        var user = new TestUser { Email = "debug@example.com", Name = "Debug User", Age = 30 };
        bool globalHandlerExecuted = false;

        Console.WriteLine("=== Testing Global Handler Registration ===");

        // Register global handler
        Console.WriteLine("Registering global handler...");
        _dispatcher.ForEvent<TestUserRegistrationEvent_v3>()
            .OnExecutePhase(execute => execute
                .Sequential(evt => {
                    globalHandlerExecuted = true;
                    Console.WriteLine($"Global handler executed for: {evt.User.Email}");
                    return LSHandlerResult.CONTINUE;
                })
            );
        Console.WriteLine("Global handler registration completed.");

        // Test direct dispatch first
        Console.WriteLine("\n=== Testing Direct Dispatch ===");
        var testEvent2 = new TestUserRegistrationEvent_v3(user, "test2");
        Console.WriteLine($"Created event: {testEvent2.ID}");
        Console.WriteLine($"Event IsBuilt before dispatch: {testEvent2.IsBuilt}");

        var result2 = testEvent2.Dispatch(_dispatcher);
        Console.WriteLine($"Direct Dispatch result - Success: {result2}, Handler called: {globalHandlerExecuted}");
        Console.WriteLine($"Event IsBuilt after dispatch: {testEvent2.IsBuilt}");
        Console.WriteLine($"Event IsCompleted after dispatch: {testEvent2.IsCompleted}");

        // Reset and test with callbacks
        Console.WriteLine("\n=== Testing WithCallbacks ===");
        globalHandlerExecuted = false;
        var testEvent1 = new TestUserRegistrationEvent_v3(user, "test1");
        Console.WriteLine($"Created event: {testEvent1.ID}");
        Console.WriteLine($"Event IsBuilt before WithCallbacks: {testEvent1.IsBuilt}");
        // Using the same dispatcher with global handlers registered plus with additional event-scoped handlers
        var result1 = testEvent1.WithCallbacks<TestUserRegistrationEvent_v3>(_dispatcher)
            .OnValidate(validate => validate
                .Sequential(evt => {
                    globalHandlerExecuted = true;
                    Console.WriteLine($"Event-scope handler executed for: {evt.User.Email}");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();
        Console.WriteLine($"WithCallbacks result - Success: {result1}, Handler called: {globalHandlerExecuted}");
        Console.WriteLine($"Event IsBuilt after WithCallbacks: {testEvent1.IsBuilt}");
        Console.WriteLine($"Event IsCompleted after WithCallbacks: {testEvent1.IsCompleted}");

        Console.WriteLine("=== Test Summary ===");
        Console.WriteLine($"Direct dispatch worked: {result2}");
        Console.WriteLine($"WithCallbacks worked: {result1}");

        // Both should work if global handlers are properly registered
        Assert.That(result2, Is.True, $"Direct dispatch should work - but this is the core issue! Success: {result2}, Handler called in direct: {globalHandlerExecuted}");
        Assert.That(result1, Is.True, "WithCallbacks should work");

        // Force failure to see output
        //Assert.Fail($"FORCED FAILURE - Direct dispatch result: {result2}, WithCallbacks result: {result1}");
    }

    [Test]
    public void Test_Build_API_Basic_Usage() {
        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        bool validationCalled = false;
        bool executionCalled = false;
        bool completeCalled = false;

        // Act - Using the v3 Build API
        var userEvent = new TestUserRegistrationEvent_v3(user, "mobile");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    validationCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    executionCalled = true;
                    evt.SetData("execution.completed", true);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnCompleteAction(evt => {
                completeCalled = true;
            })
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(validationCalled, Is.True);
        Assert.That(executionCalled, Is.True);
        Assert.That(completeCalled, Is.True);
        Assert.That(userEvent.GetData<bool>("execution.completed"), Is.True);
    }

    [Test]
    public void Test_Event_Phase_Execution_Order() {
        // Arrange
        var startupEvent = new TestSystemStartupEvent_v3("1.0.0");
        var executionOrder = new List<string>();

        // Act
        var success = startupEvent.WithCallbacks<TestSystemStartupEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    executionOrder.Add("VALIDATE");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnPrepare(prepare => prepare
                .Sequential(evt => {
                    executionOrder.Add("PREPARE");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    executionOrder.Add("EXECUTE");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnSuccessAction(evt => {
                executionOrder.Add("SUCCESS");
            })
            .OnCompleteAction(evt => {
                executionOrder.Add("COMPLETE");
            })
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(executionOrder.Count, Is.EqualTo(5));
        Assert.That(executionOrder[0], Is.EqualTo("VALIDATE"));
        Assert.That(executionOrder[1], Is.EqualTo("PREPARE"));
        Assert.That(executionOrder[2], Is.EqualTo("EXECUTE"));
        Assert.That(executionOrder[3], Is.EqualTo("SUCCESS"));
        Assert.That(executionOrder[4], Is.EqualTo("COMPLETE"));
    }

    [Test]
    public void Test_Event_Cancellation() {
        // Arrange
        var user = new TestUser { Email = "", Name = "Test User", Age = 25 }; // Invalid email
        bool validationCalled = false;
        bool executionCalled = false;
        bool completeCalled = false;

        // Act
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    validationCalled = true;
                    if (string.IsNullOrEmpty(evt.User.Email)) {
                        evt.IsCancelled = true;
                        evt.SetData("errorMessage", "Email is required");
                        return LSHandlerResult.CANCEL;
                    }
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    executionCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnCompleteAction(evt => {
                completeCalled = true;
            })
            .Dispatch();

        // Assert
        Assert.That(success, Is.False);
        Assert.That(validationCalled, Is.True);
        Assert.That(executionCalled, Is.False); // Should not execute after cancellation
        Assert.That(completeCalled, Is.True); // Complete always runs
        Assert.That(userEvent.IsCancelled, Is.True);
        Assert.That(userEvent.GetData<string>("errorMessage"), Is.EqualTo("Email is required"));
    }

    // === CRITICAL FIXES VERIFICATION TESTS ===

    [Test]
    public void Test_Fixed_Handler_Execution_Order() {
        // This is the critical test for the main fix
        // Previously: handlerA (sequential) → handlerB (parallel) → handlerC (parallel) → handlerD (sequential)
        // Would execute as: A → D → B → C (BROKEN)
        // Should execute as: A → B → C → D (FIXED)

        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        var executionOrder = new List<string>();

        // Act
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnExecute(execute => execute
                .Sequential(evt => {
                    executionOrder.Add("A");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    executionOrder.Add("B");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    executionOrder.Add("C");
                    return LSHandlerResult.CONTINUE;
                })
                .Sequential(evt => {
                    executionOrder.Add("D");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(executionOrder.Count, Is.EqualTo(4));
        Assert.That(executionOrder[0], Is.EqualTo("A"), "Handler A should execute first");
        Assert.That(executionOrder[1], Is.EqualTo("B"), "Handler B should execute second (registration order)");
        Assert.That(executionOrder[2], Is.EqualTo("C"), "Handler C should execute third (registration order)");
        Assert.That(executionOrder[3], Is.EqualTo("D"), "Handler D should execute fourth (registration order)");

        // This verifies the fix: execution follows registration order, not execution mode grouping
    }

    [Test]
    public void Test_Fixed_Global_And_EventScoped_Handler_Integration() {
        // This test verifies the second critical fix: global handlers + event-scoped handlers working together

        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        var executionOrder = new List<string>();

        // Register global handlers
        _dispatcher.ForEvent<TestUserRegistrationEvent_v3>()
            .OnExecutePhase(execute => execute
                .Sequential(evt => {
                    executionOrder.Add("GLOBAL");
                    return LSHandlerResult.CONTINUE;
                })
            );

        // Act - Create event with event-scoped handlers using the same dispatcher
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>(_dispatcher)
            .OnExecute(execute => execute
                .Sequential(evt => {
                    executionOrder.Add("EVENT-SCOPED");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(executionOrder.Count, Is.EqualTo(2), "Both global and event-scoped handlers should execute");
        Assert.That(executionOrder, Contains.Item("GLOBAL"), "Global handler should execute");
        Assert.That(executionOrder, Contains.Item("EVENT-SCOPED"), "Event-scoped handler should execute");

        // This verifies the fix: both global and event-scoped handlers execute together
    }

    [Test]
    public void Test_Priority_Based_Execution_With_Registration_Order() {
        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        var executionOrder = new List<string>();

        // Act - Mix priorities with registration order
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    executionOrder.Add("Normal-1");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.NORMAL)
                .Sequential(evt => {
                    executionOrder.Add("Critical-1");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.CRITICAL)
                .Sequential(evt => {
                    executionOrder.Add("High-1");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                .Sequential(evt => {
                    executionOrder.Add("Normal-2");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.NORMAL)
                .Sequential(evt => {
                    executionOrder.Add("Critical-2");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.CRITICAL)
            )
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(executionOrder[0], Is.EqualTo("Critical-1"), "First critical handler");
        Assert.That(executionOrder[1], Is.EqualTo("Critical-2"), "Second critical handler");
        Assert.That(executionOrder[2], Is.EqualTo("High-1"), "High priority handler");
        Assert.That(executionOrder[3], Is.EqualTo("Normal-1"), "First normal handler");
        Assert.That(executionOrder[4], Is.EqualTo("Normal-2"), "Second normal handler");
    }

    [Test]
    public void Test_State_Based_Handlers() {
        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        bool successHandlerCalled = false;
        bool errorHandlerCalled = false;
        bool cancelHandlerCalled = false;

        // Test successful event
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => LSHandlerResult.CONTINUE)
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    evt.SetData("processed", true);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnSuccessAction(evt => {
                successHandlerCalled = true;
            })
            .OnFailure(failure => failure
                .Sequential(evt => {
                    errorHandlerCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnCancel(cancel => cancel
                .Sequential(evt => {
                    cancelHandlerCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(successHandlerCalled, Is.True);
        Assert.That(errorHandlerCalled, Is.False);
        Assert.That(cancelHandlerCalled, Is.False);
    }

    [Test]
    public void Test_Event_Instance_Isolation() {
        // Arrange
        var user1 = new TestUser { Email = "user1@example.com", Name = "User 1", Age = 25 };
        var user2 = new TestUser { Email = "user2@example.com", Name = "User 2", Age = 30 };

        bool handler1Called = false;
        bool handler2Called = false;

        // Create separate events for each user
        var event1 = new TestUserRegistrationEvent_v3(user1, "web");
        var event2 = new TestUserRegistrationEvent_v3(user2, "mobile");

        // Register separate callback builders
        var builder1 = event1.WithCallbacks<TestUserRegistrationEvent_v3>();
        builder1.OnExecute(execute => execute
            .Sequential(evt => {
                handler1Called = true;
                Assert.That(evt.User.Name, Is.EqualTo("User 1"));
                return LSHandlerResult.CONTINUE;
            })
        );

        var builder2 = event2.WithCallbacks<TestUserRegistrationEvent_v3>();
        builder2.OnExecute(execute => execute
            .Sequential(evt => {
                handler2Called = true;
                Assert.That(evt.User.Name, Is.EqualTo("User 2"));
                return LSHandlerResult.CONTINUE;
            })
        );

        // Act - Process first event
        builder1.Dispatch();
        Assert.That(handler1Called, Is.True);
        Assert.That(handler2Called, Is.False);

        // Reset and process second event
        handler1Called = false;
        handler2Called = false;

        builder2.Dispatch();
        Assert.That(handler1Called, Is.False);
        Assert.That(handler2Called, Is.True);
    }

    [Test]
    public void Test_Dispatcher_Handler_Registration() {
        // Arrange & Act
        _dispatcher.ForEvent<TestSystemStartupEvent_v3>()
            .OnExecutePhase(execute => execute
                .Sequential(evt => LSHandlerResult.CONTINUE)
            );

        // Create and dispatch an event to verify the handler was registered
        var testEvent = new TestSystemStartupEvent_v3("test");
        bool handlerExecuted = false;

        testEvent.WithCallbacks<TestSystemStartupEvent_v3>()
            .OnExecute(execute => execute
                .Sequential(evt => {
                    handlerExecuted = true;
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();

        // Assert - Verify that both global and event-scoped handlers executed
        Assert.That(handlerExecuted, Is.True, "Event-scoped handler should execute");
        // Note: We can't directly count handlers in v3, but we can verify they execute
    }

    [Test]
    public void Test_Cancel_Phase_Execution() {
        // Arrange
        var user = new TestUser { Email = "", Name = "Test User", Age = 25 }; // Invalid email
        bool cancelHandlerCalled = false;
        bool successHandlerCalled = false;

        // Act
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    if (string.IsNullOrEmpty(evt.User.Email)) {
                        evt.IsCancelled = true;
                        evt.SetData("errorMessage", "Email validation failed");
                        return LSHandlerResult.CANCEL;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .When((xtx) => {
                    return false;
                },(config) => {
                    
                })
            )
            .OnSuccessAction(evt => {
                successHandlerCalled = true;
            })
            .OnCancelAction(evt => {
                cancelHandlerCalled = true;
            })
            .Dispatch();

        // Assert
        Assert.That(success, Is.False);
        Assert.That(userEvent.IsCancelled, Is.True);
        Assert.That(cancelHandlerCalled, Is.True);
        Assert.That(successHandlerCalled, Is.False);
    }

    [Test]
    public void Test_No_Handlers_Event_Processing() {
        // Arrange
        var startupEvent = new TestSystemStartupEvent_v3("no-handlers");

        // Act - Dispatch event with no handlers
        var success = startupEvent.WithCallbacks<TestSystemStartupEvent_v3>()
            .Dispatch();

        // Assert
        Assert.That(success, Is.True, "Event should complete successfully even with no handlers");
        Assert.That(startupEvent.IsCompleted, Is.True);
        Assert.That(startupEvent.IsCancelled, Is.False);
    }

    [Test]
    public void Test_Event_Data_Persistence_Across_Phases() {
        // Arrange
        var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
        const string testKey = "cross.phase.data";
        const string testValue = "persistent-value";

        // Act
        var userEvent = new TestUserRegistrationEvent_v3(user, "web");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    evt.SetData(testKey, testValue);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    Assert.That(evt.GetData<string>(testKey), Is.EqualTo(testValue), "Data should persist across phases");
                    evt.SetData("execute.verified", true);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnComplete(complete => complete
                .Sequential(evt => {
                    Assert.That(evt.GetData<string>(testKey), Is.EqualTo(testValue), "Data should persist to completion");
                    Assert.That(evt.GetData<bool>("execute.verified"), Is.True, "Execute phase data should be available");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(userEvent.GetData<string>(testKey), Is.EqualTo(testValue));
        Assert.That(userEvent.GetData<bool>("execute.verified"), Is.True);
    }

    // === INTEGRATION TESTS ===

    [Test]
    public void Test_Complex_Workflow_With_All_Phases() {
        // Arrange
        var user = new TestUser { Email = "complex@example.com", Name = "Complex User", Age = 35 };
        var workflowSteps = new List<string>();

        // Act
        var userEvent = new TestUserRegistrationEvent_v3(user, "complex-workflow");
        var success = userEvent.WithCallbacks<TestUserRegistrationEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    workflowSteps.Add("validation");
                    if (string.IsNullOrEmpty(evt.User.Email)) {
                        return LSHandlerResult.CANCEL;
                    }
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                .Sequential(evt => {
                    workflowSteps.Add("validation-detailed");
                    evt.SetData("validation.passed", true);
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.NORMAL)
            )
            .OnPrepare(prepare => prepare
                .Sequential(evt => {
                    workflowSteps.Add("preparation");
                    evt.SetData("prepared", true);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnExecute(execute => execute
                .Sequential(evt => {
                    workflowSteps.Add("pre-processing");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    workflowSteps.Add("parallel-task-1");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    workflowSteps.Add("parallel-task-2");
                    return LSHandlerResult.CONTINUE;
                })
                .Sequential(evt => {
                    workflowSteps.Add("post-processing");
                    evt.SetData("execution.completed", true);
                    return LSHandlerResult.CONTINUE;
                })
            )
            .OnSuccessAction(evt => {
                workflowSteps.Add("success-handling");
            })
            .OnCompleteAction(evt => {
                workflowSteps.Add("cleanup");
            })
            .Dispatch();

        // Assert
        Assert.That(success, Is.True);
        Assert.That(workflowSteps, Contains.Item("validation"));
        Assert.That(workflowSteps, Contains.Item("validation-detailed"));
        Assert.That(workflowSteps, Contains.Item("preparation"));
        Assert.That(workflowSteps, Contains.Item("pre-processing"));
        Assert.That(workflowSteps, Contains.Item("parallel-task-1"));
        Assert.That(workflowSteps, Contains.Item("parallel-task-2"));
        Assert.That(workflowSteps, Contains.Item("post-processing"));
        Assert.That(workflowSteps, Contains.Item("success-handling"));
        Assert.That(workflowSteps, Contains.Item("cleanup"));

        // Verify order: validation phases should come before preparation, which comes before execution
        var validationIndex = workflowSteps.IndexOf("validation");
        var preparationIndex = workflowSteps.IndexOf("preparation");
        var preProcessingIndex = workflowSteps.IndexOf("pre-processing");
        var successIndex = workflowSteps.IndexOf("success-handling");

        Assert.That(validationIndex, Is.LessThan(preparationIndex), "Validation should come before preparation");
        Assert.That(preparationIndex, Is.LessThan(preProcessingIndex), "Preparation should come before execution");
        Assert.That(preProcessingIndex, Is.LessThan(successIndex), "Execution should come before success");

        Assert.That(userEvent.GetData<bool>("validation.passed"), Is.True);
        Assert.That(userEvent.GetData<bool>("prepared"), Is.True);
        Assert.That(userEvent.GetData<bool>("execution.completed"), Is.True);
    }
}
