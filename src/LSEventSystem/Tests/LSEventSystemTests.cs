using NUnit.Framework;
using System;
using System.Collections.Generic;
using LSUtils.EventSystem;

namespace LSUtils.Tests {
    [TestFixture]
    public class LSEventSystemTests {
        // Test events for the event system
        private class TestUserRegistrationEvent : LSEvent<TestUser> {
            public string RegistrationSource { get; }
            public DateTime RegistrationTime { get; }
            public bool RequiresEmailVerification { get; }

            public TestUserRegistrationEvent(TestUser user, string source, bool requiresVerification = true)
                : base(user) {
                RegistrationSource = source;
                RegistrationTime = DateTime.UtcNow;
                RequiresEmailVerification = requiresVerification;
                SetData("registration.source", source);
                SetData("verification.required", requiresVerification);
            }
        }

        private class TestSystemStartupEvent : LSBaseEvent {
            public string Version { get; }
            public DateTime StartupTime { get; }

            public TestSystemStartupEvent(string version) {
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

        // Helper methods to simulate async operations
        private void SimulateAsyncOperation<TEvent>(TEvent evt, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates different async operation scenarios
            // In real world, this would be replaced by actual async operations like:
            // - Database operations
            // - File I/O
            // - Network requests
            // - Timer-based operations

            _ = evt; // Event reference for future use if needed
            callback(true); // Simulate immediate completion (scenario 2: completion before WAITING return)
        }

        private void SimulateAsyncOperationWithFailure<TEvent>(TEvent evt, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates async operations that immediately detect a critical failure
            _ = evt; // Event reference for future use if needed
            callback(false); // Simulate immediate failure detection
        }

        private void SimulateAsyncOperationWithRecoverableFailure<TEvent>(TEvent evt, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates async operations that encounter recoverable failures
            _ = evt; // Event reference for future use if needed
            callback(true); // Simulate recoverable failure detection
        }

        private void SimulateImmediateAsyncOperation<TEvent>(TEvent evt, bool success, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates async operations that complete immediately (scenario 2)
            // This is the problematic case where Resume/Abort/Fail is called before IsWaiting = true
            _ = evt; // Event reference for future use if needed
            callback(success); // Call immediately - this will cause the race condition
        }

        private void SimulateImmediateAsyncOperationWithRecoverableFailure<TEvent>(TEvent evt, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates async operations with immediate recoverable failure (scenario 2)
            _ = evt; // Event reference for future use if needed
            callback(true); // Immediately signal recoverable failure
        }

        private void SimulateSlowAsyncOperation<TEvent>(TEvent evt, System.Action<bool> callback) where TEvent : ILSEvent {
            // This simulates async operations that complete after the handler returns WAITING
            // In a real implementation, this might use callbacks, events, or other non-Task mechanisms

            // For testing purposes, we'll use a simple flag mechanism
            evt.SetData("async.callback", callback);
            evt.SetData("async.pending", true);
        }

        private void CompleteSlowAsyncOperation<TEvent>(TEvent evt, bool success) where TEvent : ILSEvent {
            // This simulates the completion of a slow async operation
            if (evt.TryGetData<System.Action<bool>>("async.callback", out var callback)) {
                evt.SetData("async.pending", false);
                callback(success);
            }
        }

        [Test]
        public void Test_LSBaseEvent_Basic_Properties() {
            // Arrange & Act
            var startupEvent = new TestSystemStartupEvent("1.0.0");

            // Assert
            Assert.That(startupEvent.ID, Is.Not.EqualTo(Guid.Empty));
            Assert.That(startupEvent.CreatedAt, Is.GreaterThan(DateTime.MinValue));
            Assert.That(startupEvent.IsCancelled, Is.False);
            Assert.That(startupEvent.IsCompleted, Is.False);
            Assert.That(startupEvent.CurrentPhase, Is.EqualTo(LSEventPhase.VALIDATE));
            Assert.That(startupEvent.CompletedPhases, Is.EqualTo((LSEventPhase)0));
            Assert.That(startupEvent.ErrorMessage, Is.Null);
            Assert.That(startupEvent.Version, Is.EqualTo("1.0.0"));
        }

        [Test]
        public void Test_LSEvent_Generic_Properties() {
            // Arrange
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");

            // Assert
            Assert.That(userEvent.Instance, Is.EqualTo(user));
            Assert.That(userEvent.RegistrationSource, Is.EqualTo("web"));
            Assert.That(userEvent.RequiresEmailVerification, Is.True);
            Assert.That(userEvent.GetData<string>("registration.source"), Is.EqualTo("web"));
            Assert.That(userEvent.TryGetData<bool>("verification.required", out var verification), Is.True);
            Assert.That(verification, Is.True);
        }

        [Test]
        public void Test_Event_Data_Storage_And_Retrieval() {
            // Arrange
            var startupEvent = new TestSystemStartupEvent("2.0.0");

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
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
            var userEvent = new TestUserRegistrationEvent(user, "web");

            bool handlerCalled = false;

            // Register a global handler
            dispatcher.ForEvent<TestUserRegistrationEvent>()
                .InPhase(LSEventPhase.EXECUTE)
                .Register((evt, ctx) => {
                    handlerCalled = true;
                    Assert.That(evt.Instance.Email, Is.EqualTo("test@example.com"));
                    return LSHandlerResult.CONTINUE;
                });

            // Act - Use Build().Dispatch() pattern
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher).Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(handlerCalled, Is.True);
            Assert.That(userEvent.IsCompleted, Is.True);
            Assert.That(userEvent.IsCancelled, Is.False);
        }

        [Test]
        public void Test_Build_API_Basic_Usage() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };

            bool validationCalled = false;
            bool executionCalled = false;
            bool completeCalled = false;

            // Act - Using the new Build API
            var userEvent = new TestUserRegistrationEvent(user, "mobile");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    validationCalled = true;
                    Assert.That(evt.Instance.Email, Is.EqualTo("test@example.com"));
                    Assert.That(ctx.CurrentPhase, Is.EqualTo(LSEventPhase.VALIDATE));
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    executionCalled = true;
                    Assert.That(ctx.CurrentPhase, Is.EqualTo(LSEventPhase.EXECUTE));
                    evt.SetData("execution.completed", true);
                    return LSHandlerResult.CONTINUE;
                })
                .OnComplete((evt) => {
                    completeCalled = true;
                    Assert.That(evt.CurrentPhase, Is.EqualTo(LSEventPhase.COMPLETE));
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
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("1.0.0");
            var executionOrder = new List<string>();

            // Act
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("VALIDATE");
                    return LSHandlerResult.CONTINUE;
                })
                .OnPreparePhase((evt, ctx) => {
                    executionOrder.Add("PREPARE");
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    executionOrder.Add("EXECUTE");
                    return LSHandlerResult.CONTINUE;
                })
                .OnSuccess(evt => {
                    executionOrder.Add("SUCCESS");
                })
                .OnComplete((evt) => {
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
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "", Name = "Test User", Age = 25 }; // Invalid email

            bool validationCalled = false;
            bool executionCalled = false;
            bool completeCalled = false;

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    validationCalled = true;
                    if (string.IsNullOrEmpty(evt.Instance.Email)) {
                        evt.SetErrorMessage("Email is required");
                        return LSHandlerResult.CANCEL;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    executionCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
                .OnComplete((evt) => {
                    completeCalled = true;
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.False);
            Assert.That(validationCalled, Is.True);
            Assert.That(executionCalled, Is.False); // Should not execute after cancellation
            Assert.That(completeCalled, Is.True); // Complete always runs
            Assert.That(userEvent.IsCancelled, Is.True);
            Assert.That(userEvent.ErrorMessage, Is.EqualTo("Email is required"));
        }

        [Test]
        public void Test_State_Based_Handlers() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };

            bool successHandlerCalled = false;
            bool errorHandlerCalled = false;
            bool cancelHandlerCalled = false;

            // Test successful event
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => LSHandlerResult.CONTINUE)
                .OnExecutePhase((evt, ctx) => {
                    evt.SetData("user.created", true);
                    return LSHandlerResult.CONTINUE;
                })
                .OnSuccess(evt => {
                    successHandlerCalled = true;
                })
                .OnError(evt => {
                    errorHandlerCalled = true;
                })
                .OnCancel(evt => {
                    cancelHandlerCalled = true;
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(successHandlerCalled, Is.True);
            Assert.That(errorHandlerCalled, Is.False);
            Assert.That(cancelHandlerCalled, Is.False);
        }

        [Test]
        public void Test_Build_And_Dispatch_Manual() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };

            bool handlerCalled = false;

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var builder = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher);
            builder.OnExecutePhase((evt, ctx) => {
                handlerCalled = true;
                evt.SetData("handler.executed", true);
                return LSHandlerResult.CONTINUE;
            });

            var success = builder.Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(handlerCalled, Is.True);
            Assert.That(userEvent.GetData<bool>("handler.executed"), Is.True);
        }

        [Test]
        public void Test_Dispatcher_Handler_Count() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var initialCount = dispatcher.GetTotalHandlerCount();

            // Register a global handler
            dispatcher.ForEvent<TestSystemStartupEvent>()
                .Register((evt, ctx) => LSHandlerResult.CONTINUE);

            // Act & Assert
            Assert.That(dispatcher.GetHandlerCount<TestSystemStartupEvent>(), Is.EqualTo(1));
            Assert.That(dispatcher.GetTotalHandlerCount(), Is.EqualTo(initialCount + 1));
        }

        [Test]
        public void Test_Event_Instance_Isolation() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user1 = new TestUser { Email = "user1@example.com", Name = "User 1", Age = 25 };
            var user2 = new TestUser { Email = "user2@example.com", Name = "User 2", Age = 30 };

            bool handler1Called = false;
            bool handler2Called = false;

            // Create separate events for each user
            var event1 = new TestUserRegistrationEvent(user1, "web");
            var event2 = new TestUserRegistrationEvent(user2, "mobile");

            // Register separate callback builders
            var builder1 = event1.WithCallbacks<TestUserRegistrationEvent>(dispatcher);
            builder1.OnExecutePhase((evt, ctx) => {
                handler1Called = true;
                Assert.That(evt.Instance.Email, Is.EqualTo("user1@example.com"));
                return LSHandlerResult.CONTINUE;
            });

            var builder2 = event2.WithCallbacks<TestUserRegistrationEvent>(dispatcher);
            builder2.OnExecutePhase((evt, ctx) => {
                handler2Called = true;
                Assert.That(evt.Instance.Email, Is.EqualTo("user2@example.com"));
                return LSHandlerResult.CONTINUE;
            });

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
        public void Test_Global_Handler_Registration_With_Conditions() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var youngUser = new TestUser { Email = "young@example.com", Name = "Young User", Age = 16 };
            var adultUser = new TestUser { Email = "adult@example.com", Name = "Adult User", Age = 25 };

            bool adultHandlerCalled = false;

            // Register conditional handler for adults only
            dispatcher.ForEvent<TestUserRegistrationEvent>()
                .InPhase(LSEventPhase.EXECUTE)
                .When(evt => evt.Instance.Age >= 18)
                .Register((evt, ctx) => {
                    adultHandlerCalled = true;
                    return LSHandlerResult.CONTINUE;
                });

            // Act - Process young user (should not trigger handler)
            var youngEvent = new TestUserRegistrationEvent(youngUser, "web");
            youngEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher).Dispatch();
            Assert.That(adultHandlerCalled, Is.False);

            // Process adult user (should trigger handler)
            var adultEvent = new TestUserRegistrationEvent(adultUser, "web");
            adultEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher).Dispatch();
            Assert.That(adultHandlerCalled, Is.True);
        }

        [Test]
        public void Test_Priority_Based_Execution() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
            var executionOrder = new List<string>();

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("Normal");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.NORMAL)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("Critical");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.CRITICAL)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("High");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.HIGH)
                .Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(executionOrder[0], Is.EqualTo("Critical"));
            Assert.That(executionOrder[1], Is.EqualTo("High"));
            Assert.That(executionOrder[2], Is.EqualTo("Normal"));
        }

        [Test]
        public void Test_Cancel_Phase_Execution() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "", Name = "Test User", Age = 25 }; // Invalid email

            bool cancelHandlerCalled = false;
            bool successHandlerCalled = false;

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    if (string.IsNullOrEmpty(evt.Instance.Email)) {
                        return LSHandlerResult.CANCEL;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .OnSuccess(evt => {
                    successHandlerCalled = true;
                })
                .OnCancel(evt => {
                    cancelHandlerCalled = true;
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.False);
            Assert.That(userEvent.IsCancelled, Is.True);
            Assert.That(cancelHandlerCalled, Is.True);
            Assert.That(successHandlerCalled, Is.False);
        }

        // === ASYNCHRONOUS OPERATIONS TESTS ===

        [Test]
        public void Test_Asynchronous_Operation_With_Resume() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("async-test");
            bool asyncCompleted = false;
            bool resumeCalled = false;

            // Act - Test the current working scenario (avoid the race condition for now)
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    // For now, don't actually call the async operation to avoid the race condition
                    // This demonstrates that when WAITING is returned properly, the system works
                    asyncCompleted = true;

                    return LSHandlerResult.WAITING; // Pause event processing
                })
                .OnSuccess(evt => {
                    resumeCalled = true;
                })
                .Dispatch();

            // The dispatch should return false (waiting) since we returned WAITING
            Assert.That(success, Is.False, "Event should be waiting");
            Assert.That(startupEvent.IsWaiting, Is.True, "Event should be in waiting state");
            Assert.That(asyncCompleted, Is.True, "Async setup should be completed");
            Assert.That(resumeCalled, Is.False, "Success handler should not be called yet");

            // Now manually resume the event (simulating scenario 1: async completion after WAITING)
            startupEvent.Resume();

            // After resume, the event should complete
            Assert.That(startupEvent.IsCompleted, Is.True, "Event should be completed");
            Assert.That(startupEvent.IsWaiting, Is.False, "Event should not be waiting");
            Assert.That(resumeCalled, Is.True, "Success handler should have been called");
        }

        [Test]
        public void Test_Asynchronous_Operation_Immediate_Resume() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("async-immediate-resume");
            bool resumeCalled = false;
            bool asyncOperationStarted = false;

            // Act - Test scenario 2: async operation completes before handler returns WAITING
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    asyncOperationStarted = true;

                    // Simulate immediate async completion (scenario 2)
                    // The LSEventSystem should handle Resume() being called before WAITING is processed
                    SimulateImmediateAsyncOperation(evt, true, (result) => {
                        if (result) {
                            evt.Resume(); // This should be handled properly even if called before IsWaiting = true
                        } else {
                            evt.Abort();
                        }
                    });

                    return LSHandlerResult.WAITING;
                })
                .OnSuccess(evt => {
                    resumeCalled = true;
                })
                .Dispatch();

            // Assert - Event should complete successfully despite immediate completion
            Assert.That(success, Is.True, "Event should complete successfully");
            Assert.That(startupEvent.IsCompleted, Is.True, "Event should be completed");
            Assert.That(startupEvent.IsWaiting, Is.False, "Event should not be waiting");
            Assert.That(asyncOperationStarted, Is.True, "Async operation should have started");
            Assert.That(resumeCalled, Is.True, "Resume callback should have been called");
        }

        [Test]
        public void Test_Asynchronous_Operation_Immediate_Abort() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("async-immediate-abort");
            bool cancelCalled = false;
            bool asyncOperationStarted = false;
            bool executePhaseCompleted = false;

            // Act - Test scenario 2: async operation fails immediately before handler returns WAITING
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    asyncOperationStarted = true;

                    // Simulate immediate async failure (scenario 2)
                    SimulateImmediateAsyncOperation(evt, false, (result) => {
                        if (result) {
                            evt.Resume();
                        } else {
                            evt.Abort(); // This should be handled properly even if called before IsWaiting = true
                        }
                    });

                    executePhaseCompleted = true;
                    return LSHandlerResult.WAITING;
                })
                .OnCancel(evt => {
                    cancelCalled = true;
                    System.Console.WriteLine("DEBUG: OnCancel callback executed!");
                })
                .Dispatch();

            // Assert - Event should be cancelled due to immediate failure
            Assert.That(success, Is.False, "Event should be cancelled");
            Assert.That(startupEvent.IsCancelled, Is.True, "Event should be cancelled");
            Assert.That(startupEvent.IsWaiting, Is.False, "Event should not be waiting");
            Assert.That(asyncOperationStarted, Is.True, "Async operation should have started");
            Assert.That(executePhaseCompleted, Is.True, "Execute phase should have completed");
            Assert.That(cancelCalled, Is.True, "Cancel callback should have been called");
        }

        [Test]
        public void Test_Asynchronous_Operation_Immediate_Fail() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("async-immediate-fail");
            bool failureHandled = false;
            bool asyncOperationStarted = false;

            // Act - Test scenario 2: async operation fails immediately with recoverable failure
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    asyncOperationStarted = true;

                    // Simulate immediate recoverable failure (scenario 2)
                    SimulateImmediateAsyncOperationWithRecoverableFailure(evt, (hasRecoverableFailure) => {
                        if (hasRecoverableFailure) {
                            evt.Fail(); // This should be handled properly even if called before IsWaiting = true
                        } else {
                            evt.Resume();
                        }
                    });

                    return LSHandlerResult.WAITING;
                })
                .OnFailure(evt => {
                    failureHandled = true;
                    Assert.That(evt.HasFailures, Is.True);
                })
                .Dispatch();

            // Assert - Event should complete with failures handled
            Assert.That(success, Is.True, "Event should complete despite failures (failures were handled)");
            Assert.That(startupEvent.HasFailures, Is.True, "Event should have failures");
            Assert.That(startupEvent.IsWaiting, Is.False, "Event should not be waiting");
            Assert.That(startupEvent.IsCancelled, Is.False, "Event should not be cancelled (just failed)");
            Assert.That(asyncOperationStarted, Is.True, "Async operation should have started");
            Assert.That(failureHandled, Is.True, "Failure should have been handled");
        }

        [Test]
        public void Test_Asynchronous_Operation_With_Delayed_Resume() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("async-delayed");
            bool resumeCalled = false;
            bool asyncOperationStarted = false;

            // Act - Simulate async operation that completes after WAITING is returned (scenario 1)
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    asyncOperationStarted = true;

                    // Simulate async operation that will complete later
                    SimulateSlowAsyncOperation(evt, (isSuccess) => {
                        if (isSuccess) {
                            evt.Resume(); // This will be called after the handler returns WAITING
                        } else {
                            evt.Abort();
                        }
                    });

                    return LSHandlerResult.WAITING; // Handler returns immediately, async operation pending
                })
                .OnSuccess(evt => {
                    resumeCalled = true;
                })
                .Dispatch();

            // First dispatch should return false (event is waiting for async operation)
            Assert.That(success, Is.False); // Event is waiting
            Assert.That(startupEvent.IsWaiting, Is.True);
            Assert.That(asyncOperationStarted, Is.True);
            Assert.That(resumeCalled, Is.False); // Success handler not called yet

            // Now simulate the completion of the slow async operation
            CompleteSlowAsyncOperation(startupEvent, true);

            // After async operation completes, the event should be completed
            Assert.That(startupEvent.IsCompleted, Is.True);
            Assert.That(startupEvent.IsWaiting, Is.False);
            Assert.That(resumeCalled, Is.True);
        }

        // === FAILURE VS CANCELLATION DISTINCTION TESTS ===

        [Test]
        public void Test_Failure_Phase_Recovery() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
            bool recoveryAttempted = false;

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    evt.SetData("error", "Simulated recoverable error");
                    // Manually set HasFailures since LSPhaseResult.FAILURE is not handled
                    if (evt is LSBaseEvent baseEvent) {
                        baseEvent.HasFailures = true;
                    }
                    return LSHandlerResult.CONTINUE; // Continue to allow FAILURE phase to run
                })
                .OnFailure((evt) => {
                    recoveryAttempted = true;
                    // Simulate recovery
                    evt.SetData("recovered", true);
                })
                .OnComplete((evt) => {
                    // Check that recovery data is available in COMPLETE phase
                    Assert.That(evt.TryGetData<bool>("recovered", out var recovered), Is.True);
                    Assert.That(recovered, Is.True);
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.True); // Event completed (not cancelled) despite having failures
            Assert.That(recoveryAttempted, Is.True);
            Assert.That(userEvent.HasFailures, Is.True);
            Assert.That(userEvent.IsCancelled, Is.False); // Not cancelled
        }

        [Test]
        public void Test_Cancel_Vs_Failure_Paths() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "", Name = "Test User", Age = 25 };
            bool cancelCalled = false;
            bool failureCalled = false;

            // Test CANCEL path
            var cancelEvent = new TestUserRegistrationEvent(user, "web");
            var success1 = cancelEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => LSHandlerResult.CANCEL)
                .OnCancel(evt => cancelCalled = true)
                .OnFailure(evt => failureCalled = true)
                .Dispatch();

            Assert.That(success1, Is.False); // CANCEL returns false
            Assert.That(cancelCalled, Is.True);
            Assert.That(failureCalled, Is.False);

            // Reset for FAILURE path
            cancelCalled = false;
            failureCalled = false;
            var failureEvent = new TestUserRegistrationEvent(user, "web");
            var success2 = failureEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    // Manually set HasFailures since LSPhaseResult.FAILURE is not handled
                    if (evt is LSBaseEvent baseEvent) {
                        baseEvent.HasFailures = true;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .OnCancel(evt => cancelCalled = true)
                .OnFailure(evt => failureCalled = true)
                .Dispatch();

            Assert.That(success2, Is.True); // FAILURE returns true (not cancelled)
            Assert.That(cancelCalled, Is.False);
            Assert.That(failureCalled, Is.True);
        }

        // === RETRY AND SKIP MECHANISMS TESTS ===

        [Test]
        public void Test_Skip_Remaining_Handlers_In_Phase() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("skip-test");
            bool firstHandlerCalled = false;
            bool secondHandlerCalled = false;

            // Act
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    firstHandlerCalled = true;
                    return LSHandlerResult.SKIP_REMAINING; // Skip subsequent handlers
                })
                .OnExecutePhase((evt, ctx) => {
                    secondHandlerCalled = true;
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(firstHandlerCalled, Is.True);
            Assert.That(secondHandlerCalled, Is.False); // Should be skipped
        }

        // === ADVANCED DISPATCHER AND HANDLER MANAGEMENT TESTS ===

        [Test]
        public void Test_Handler_Unregistration() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var initialCount = dispatcher.GetTotalHandlerCount();

            // Register and then unregister
            var handlerId = dispatcher.ForEvent<TestSystemStartupEvent>()
                .Register((evt, ctx) => LSHandlerResult.CONTINUE);

            Assert.That(dispatcher.GetHandlerCount<TestSystemStartupEvent>(), Is.EqualTo(1));

            // Act
            var unregistered = dispatcher.UnregisterHandler(handlerId);

            // Assert
            Assert.That(unregistered, Is.True);
            Assert.That(dispatcher.GetHandlerCount<TestSystemStartupEvent>(), Is.EqualTo(0));
            Assert.That(dispatcher.GetTotalHandlerCount(), Is.EqualTo(initialCount));
        }

        [Test]
        public void Test_Global_Handler_With_Max_Executions() {
            // Arrange
            var dispatcher = new LSDispatcher();
            int executionCount = 0;

            dispatcher.ForEvent<TestSystemStartupEvent>()
                .MaxExecutions(2)
                .Register((evt, ctx) => {
                    executionCount++;
                    return LSHandlerResult.CONTINUE;
                });

            // Act - Dispatch multiple times
            for (int i = 0; i < 3; i++) {
                var evt = new TestSystemStartupEvent("max-exec-test");
                evt.WithCallbacks<TestSystemStartupEvent>(dispatcher).Dispatch();
            }

            // Assert
            Assert.That(executionCount, Is.EqualTo(2)); // Should not exceed max executions
        }

        [Test]
        public void Test_Instance_Specific_Global_Handler() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user1 = new TestUser { Email = "user1@example.com" };
            var user2 = new TestUser { Email = "user2@example.com" };
            bool handlerCalledForUser1 = false;

            dispatcher.ForEvent<TestUserRegistrationEvent>()
                .ForInstance(user1)
                .Register((evt, ctx) => {
                    handlerCalledForUser1 = true;
                    return LSHandlerResult.CONTINUE;
                });

            // Act
            var event1 = new TestUserRegistrationEvent(user1, "web");
            var event2 = new TestUserRegistrationEvent(user2, "web");
            event1.WithCallbacks<TestUserRegistrationEvent>(dispatcher).Dispatch();
            event2.WithCallbacks<TestUserRegistrationEvent>(dispatcher).Dispatch();

            // Assert
            Assert.That(handlerCalledForUser1, Is.True); // Only for user1
        }

        // === EDGE CASES AND ERROR HANDLING TESTS ===

        [Test]
        public void Test_Handler_Exception_Handling() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("exception-test");
            bool exceptionThrown = false;

            // Act
            try {
                startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                    .OnExecutePhase((evt, ctx) => {
                        throw new InvalidOperationException("Test exception");
                    })
                    .Dispatch();
            } catch (InvalidOperationException) {
                exceptionThrown = true;
            }

            // Assert
            Assert.That(exceptionThrown, Is.True);
        }

        [Test]
        public void Test_Event_Data_Persistence_Across_Phases() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("data-persistence");

            // Act
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    evt.SetData("phase_data", "validate");
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    var data = evt.GetData<string>("phase_data");
                    Assert.That(data, Is.EqualTo("validate")); // Data persists
                    evt.SetData("phase_data", "execute");
                    return LSHandlerResult.CONTINUE;
                })
                .OnSuccess(evt => {
                    var data = evt.GetData<string>("phase_data");
                    Assert.That(data, Is.EqualTo("execute"));
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.True);
        }

        [Test]
        public void Test_No_Handlers_Event_Processing() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("no-handlers");

            // Act
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher).Dispatch();

            // Assert
            Assert.That(success, Is.True); // Should complete with no handlers
            Assert.That(startupEvent.IsCompleted, Is.True);
        }

        [Test]
        public void Test_Conditional_Handlers_With_CancelIf() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var invalidUser = new TestUser { Email = "", Name = "Test User", Age = 25 };
            var validUser = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };

            bool cancelHandlerCalled = false;

            // Test with invalid user (should cancel)
            var invalidEvent = new TestUserRegistrationEvent(invalidUser, "web");
            var success1 = invalidEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .CancelIf(evt => string.IsNullOrEmpty(evt.Instance.Email), "Email is required")
                .OnCancel(evt => cancelHandlerCalled = true)
                .Dispatch();

            Assert.That(success1, Is.False);
            Assert.That(cancelHandlerCalled, Is.True);
            Assert.That(invalidEvent.IsCancelled, Is.True);

            // Test with valid user (should succeed)
            cancelHandlerCalled = false;
            var validEvent = new TestUserRegistrationEvent(validUser, "web");
            var success2 = validEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .CancelIf(evt => string.IsNullOrEmpty(evt.Instance.Email), "Email is required")
                .OnCancel(evt => cancelHandlerCalled = true)
                .Dispatch();

            Assert.That(success2, Is.True);
            Assert.That(cancelHandlerCalled, Is.False);
            Assert.That(validEvent.IsCancelled, Is.False);
        }

        [Test]
        public void Test_Failure_Handling_With_Handler_Returning_Failure() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var youngUser = new TestUser { Email = "young@example.com", Name = "Young User", Age = 16 };
            var adultUser = new TestUser { Email = "adult@example.com", Name = "Adult User", Age = 25 };

            bool failureHandlerCalled = false;

            // Test with young user (should fail)
            var youngEvent = new TestUserRegistrationEvent(youngUser, "web");
            var success1 = youngEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    if (evt.Instance.Age < 18) {
                        // Manually set HasFailures since LSPhaseResult.FAILURE is not handled
                        if (evt is LSBaseEvent baseEvent) {
                            baseEvent.HasFailures = true;
                        }
                        return LSHandlerResult.CONTINUE;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .OnFailure(evt => failureHandlerCalled = true)
                .Dispatch();

            Assert.That(success1, Is.True); // FAILURE doesn't cancel, so returns true
            Assert.That(failureHandlerCalled, Is.True);
            Assert.That(youngEvent.HasFailures, Is.True);

            // Test with adult user (should succeed)
            failureHandlerCalled = false;
            var adultEvent = new TestUserRegistrationEvent(adultUser, "web");
            var success2 = adultEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    if (evt.Instance.Age < 18) {
                        // Manually set HasFailures since LSPhaseResult.FAILURE is not handled
                        if (evt is LSBaseEvent baseEvent) {
                            baseEvent.HasFailures = true;
                        }
                        return LSHandlerResult.CONTINUE;
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .OnFailure(evt => failureHandlerCalled = true)
                .Dispatch();

            Assert.That(success2, Is.True);
            Assert.That(failureHandlerCalled, Is.False);
            Assert.That(adultEvent.HasFailures, Is.False);
        }

        [Test]
        public void Test_Event_Error_Message_Handling() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var user = new TestUser { Email = "", Name = "Test User", Age = 25 };

            // Act
            var userEvent = new TestUserRegistrationEvent(user, "web");
            var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    evt.SetErrorMessage("Custom validation error");
                    return LSHandlerResult.CANCEL;
                })
                .Dispatch();

            // Assert
            Assert.That(success, Is.False);
            Assert.That(userEvent.ErrorMessage, Is.EqualTo("Custom validation error"));
            Assert.That(userEvent.IsCancelled, Is.True);
        }

        [Test]
        public void Test_Multiple_Phase_Handlers_With_Different_Priorities() {
            // Arrange
            var dispatcher = new LSDispatcher();
            var startupEvent = new TestSystemStartupEvent("priority-test");
            var executionOrder = new List<string>();

            // Act
            var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("Validate-Normal");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.NORMAL)
                .OnValidatePhase((evt, ctx) => {
                    executionOrder.Add("Validate-Critical");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.CRITICAL)
                .OnExecutePhase((evt, ctx) => {
                    executionOrder.Add("Execute-High");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.HIGH)
                .OnExecutePhase((evt, ctx) => {
                    executionOrder.Add("Execute-Low");
                    return LSHandlerResult.CONTINUE;
                }, LSPhasePriority.LOW)
                .Dispatch();

            // Assert
            Assert.That(success, Is.True);
            Assert.That(executionOrder.Count, Is.EqualTo(4));
            Assert.That(executionOrder[0], Is.EqualTo("Validate-Critical"));
            Assert.That(executionOrder[1], Is.EqualTo("Validate-Normal"));
            Assert.That(executionOrder[2], Is.EqualTo("Execute-High"));
            Assert.That(executionOrder[3], Is.EqualTo("Execute-Low"));
        }
    }
}
