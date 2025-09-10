using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.EventSystem;

namespace LSUtils.EventSystem.Tests;

/// <summary>
/// Comprehensive NUnit tests for the LSEventSystem v4 clean redesign.
/// Tests the simplified state machine, priority-based execution, and clean API.
/// </summary>
[TestFixture]
public class LSEventSystem_v4_Test {

    #region Test Events

    /// <summary>
    /// Test event for user registration scenarios
    /// </summary>
    private class TestUserRegistrationEvent : BaseEvent {
        public string Email { get; }
        public string Name { get; }
        public string RegistrationSource { get; }
        public bool RequiresEmailVerification { get; }

        public TestUserRegistrationEvent(LSESDispatcher dispatcher, string email, string name, string source, bool requiresVerification = true) : base(dispatcher) {
            Email = email;
            Name = name;
            RegistrationSource = source;
            RequiresEmailVerification = requiresVerification;

            // Store initial data
            SetData("registration.source", source);
            SetData("verification.required", requiresVerification);
            SetData("user.email", email);
            SetData("user.name", name);
        }
    }

    /// <summary>
    /// Test event for system startup scenarios
    /// </summary>
    private class TestSystemStartupEvent : BaseEvent {
        public string Version { get; }
        public DateTime StartupTime { get; }

        public TestSystemStartupEvent(LSESDispatcher dispatcher, string version) : base(dispatcher) {
            Version = version;
            StartupTime = DateTime.UtcNow;
            SetData("system.version", version);
            SetData("startup.time", StartupTime);
        }
    }

    /// <summary>
    /// Test event for order processing scenarios
    /// </summary>
    private class TestOrderProcessingEvent : BaseEvent {
        public decimal Amount { get; }
        public string Currency { get; }
        public string OrderId { get; }

        public TestOrderProcessingEvent(LSESDispatcher dispatcher, decimal amount, string currency, string orderId) : base(dispatcher) {
            Amount = amount;
            Currency = currency;
            OrderId = orderId;
            SetData("order.amount", amount);
            SetData("order.currency", currency);
            SetData("order.id", orderId);
        }
    }

    #endregion

    #region Test Fixtures

    private LSESDispatcher _dispatcher;

    [SetUp]
    public void SetUp() {
        _dispatcher = new LSESDispatcher();
    }

    [TearDown]
    public void TearDown() {
        _dispatcher = null!;
    }

    #endregion

    #region Basic Event Tests

    [Test]
    public void Test_BaseEvent_Basic_Properties() {
        // Arrange & Act
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");

        // Assert
        Assert.That(userEvent.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(userEvent.CreatedAt, Is.GreaterThan(DateTime.MinValue));
        Assert.That(userEvent.IsCancelled, Is.False);
        Assert.That(userEvent.HasFailures, Is.False);
        Assert.That(userEvent.IsCompleted, Is.False);
        Assert.That(userEvent.Email, Is.EqualTo("test@example.com"));
        Assert.That(userEvent.Name, Is.EqualTo("Test User"));
        Assert.That(userEvent.RegistrationSource, Is.EqualTo("web"));
        Assert.That(userEvent.RequiresEmailVerification, Is.True);
    }

    [Test]
    public void Test_Event_Data_Storage_And_Retrieval() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "mobile");

        // Act & Assert - Initial data
        Assert.That(userEvent.GetData<string>("registration.source"), Is.EqualTo("mobile"));
        Assert.That(userEvent.GetData<bool>("verification.required"), Is.True);
        Assert.That(userEvent.GetData<string>("user.email"), Is.EqualTo("test@example.com"));

        // Act & Assert - Setting new data
        userEvent.SetData("custom.field", "custom value");
        userEvent.SetData("numeric.field", 42);
        userEvent.SetData("object.field", new { test = "value" });

        Assert.That(userEvent.GetData<string>("custom.field"), Is.EqualTo("custom value"));
        Assert.That(userEvent.GetData<int>("numeric.field"), Is.EqualTo(42));
        Assert.That(userEvent.GetData<object>("object.field"), Is.Not.Null);

        // Act & Assert - TryGetData
        Assert.That(userEvent.TryGetData<string>("custom.field", out var customValue), Is.True);
        Assert.That(customValue, Is.EqualTo("custom value"));
        Assert.That(userEvent.TryGetData<string>("non.existent", out var _), Is.False);
    }

    #endregion

    #region Dispatcher Tests

    [Test]
    public void Test_Dispatcher_Basic_Event_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var result = userEvent.Dispatch();
        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(userEvent.IsCompleted, Is.False); // No handlers registered, so just processes through
    }

    [Test]
    public void Test_Dispatcher_ForEvent_Registration() {
        // Act
        var handlerIds = _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register.OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                phaseRegister.Handler(context => {
                    return HandlerProcessResult.SUCCESS;
                }).Build()
            ).Register()
        );

        // Assert
        Assert.That(handlerIds, Is.Not.Null);
        Assert.That(handlerIds.Length, Is.EqualTo(1));
        Assert.That(handlerIds[0], Is.Not.EqualTo(Guid.Empty));

        // Verify handler is registered
        var handlers = _dispatcher.getHandlers(typeof(TestUserRegistrationEvent));
        Assert.That(handlers.Count, Is.EqualTo(1));
    }

    [Test]
    public void Test_Dispatcher_ForEventPhase_Registration() {
        // Act
        var handlerId = _dispatcher.ForEventPhase<TestUserRegistrationEvent, BusinessState.ValidatePhaseState>(register =>
            register.Handler(context => {
                return HandlerProcessResult.SUCCESS;
            }).Build()
        );

        // Assert
        Assert.That(handlerId, Is.Not.EqualTo(Guid.Empty));

        // Verify handler is registered
        var handlers = _dispatcher.getHandlers(typeof(TestUserRegistrationEvent));
        Assert.That(handlers.Count, Is.EqualTo(1));
        Assert.That(handlers[0], Is.TypeOf<PhaseHandlerEntry>());
    }

    [Test]
    public void Test_Dispatcher_ForEventState_Registration() {
        // Arrange

        // Act
        var handlerId = _dispatcher.ForEventState<TestUserRegistrationEvent, CancelledState>(register =>
            register.Handler(@event => {
                Assert.That(@event.IsCancelled, Is.True);
            }).Build()
        );

        // Assert
        Assert.That(handlerId, Is.Not.EqualTo(Guid.Empty));

        // Verify handler is registered
        var handlers = _dispatcher.getHandlers(typeof(TestUserRegistrationEvent));
        Assert.That(handlers.Count, Is.EqualTo(1));
        Assert.That(handlers[0], Is.TypeOf<StateHandlerEntry>());
    }

    #endregion

    #region State Machine Tests

    [Test]
    public void Test_BusinessState_Creation_And_Basic_Properties() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var handlers = new List<IHandlerEntry>();
        var context = EventSystemContext.Create(_dispatcher, userEvent, handlers);

        // Act
        var businessState = new BusinessState(context);

        // Assert
        Assert.That(businessState.StateResult, Is.EqualTo(StateProcessResult.UNKNOWN));
    }

    [Test]
    public void Test_EventSystemContext_Creation_And_Properties() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var handlers = new List<IHandlerEntry>();

        // Act
        var context = EventSystemContext.Create(_dispatcher, userEvent, handlers);

        // Assert
        Assert.That(context.Dispatcher, Is.EqualTo(_dispatcher));
        Assert.That(context.Event, Is.EqualTo(userEvent));
        Assert.That(context.Handlers, Is.EqualTo(handlers));
        Assert.That(context.CurrentState, Is.TypeOf<BusinessState>());
        Assert.That(context.HasFailures, Is.False);
        Assert.That(context.IsCancelled, Is.False);
    }

    [Test]
    public void Test_StateProcessResult_Enumeration_Values() {
        // Test that all expected values exist
        Assert.That(Enum.IsDefined(typeof(StateProcessResult), StateProcessResult.UNKNOWN));
        Assert.That(Enum.IsDefined(typeof(StateProcessResult), StateProcessResult.SUCCESS));
        Assert.That(Enum.IsDefined(typeof(StateProcessResult), StateProcessResult.FAILURE));
        Assert.That(Enum.IsDefined(typeof(StateProcessResult), StateProcessResult.WAITING));
        Assert.That(Enum.IsDefined(typeof(StateProcessResult), StateProcessResult.CANCELLED));
    }

    [Test]
    public void Test_HandlerProcessResult_Enumeration_Values() {
        // Test that all expected values exist
        Assert.That(Enum.IsDefined(typeof(HandlerProcessResult), HandlerProcessResult.UNKNOWN));
        Assert.That(Enum.IsDefined(typeof(HandlerProcessResult), HandlerProcessResult.SUCCESS));
        Assert.That(Enum.IsDefined(typeof(HandlerProcessResult), HandlerProcessResult.FAILURE));
        Assert.That(Enum.IsDefined(typeof(HandlerProcessResult), HandlerProcessResult.CANCELLED));
        Assert.That(Enum.IsDefined(typeof(HandlerProcessResult), HandlerProcessResult.WAITING));
    }

    [Test]
    public void Test_EventSystemPhase_Enumeration_Values() {
        // Test that all expected phases exist
        Assert.That(Enum.IsDefined(typeof(EventSystemPhase), EventSystemPhase.NONE));
        Assert.That(Enum.IsDefined(typeof(EventSystemPhase), EventSystemPhase.VALIDATE));
        Assert.That(Enum.IsDefined(typeof(EventSystemPhase), EventSystemPhase.CONFIGURE));
        Assert.That(Enum.IsDefined(typeof(EventSystemPhase), EventSystemPhase.EXECUTE));
        Assert.That(Enum.IsDefined(typeof(EventSystemPhase), EventSystemPhase.CLEANUP));

        // Test numeric values for bitwise operations
        Assert.That((int)EventSystemPhase.NONE, Is.EqualTo(0));
        Assert.That((int)EventSystemPhase.VALIDATE, Is.EqualTo(1));
        Assert.That((int)EventSystemPhase.CONFIGURE, Is.EqualTo(2));
        Assert.That((int)EventSystemPhase.EXECUTE, Is.EqualTo(4));
        Assert.That((int)EventSystemPhase.CLEANUP, Is.EqualTo(8));
    }

    #endregion

    #region Handler Entry Tests

    [Test]
    public void Test_PhaseHandlerEntry_Creation_And_Properties() {
        // Arrange & Act
        var entry = new PhaseHandlerEntry {
            PhaseType = typeof(BusinessState.ValidatePhaseState),
            Priority = LSESPriority.HIGH,
            Handler = context => HandlerProcessResult.SUCCESS,
            Condition = (evt, handlerEntry) => true,
            Description = "Test handler"
        };

        // Assert
        Assert.That(entry.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(entry.PhaseType, Is.EqualTo(typeof(BusinessState.ValidatePhaseState)));
        Assert.That(entry.Priority, Is.EqualTo(LSESPriority.HIGH));
        Assert.That(entry.Handler, Is.Not.Null);
        Assert.That(entry.Condition, Is.Not.Null);
        Assert.That(entry.Description, Is.EqualTo("Test handler"));
        Assert.That(entry.ExecutionCount, Is.EqualTo(0));
        Assert.That(entry.WaitingBlockExecution, Is.False);
    }

    [Test]
    public void Test_StateHandlerEntry_Creation_And_Properties() {
        // Arrange & Act
        var entry = new StateHandlerEntry {
            StateType = typeof(CancelledState),
            Priority = LSESPriority.CRITICAL,
            Handler = evt => { /* do nothing */ },
            Condition = (evt, handlerEntry) => evt.IsCancelled
        };

        // Assert
        Assert.That(entry.ID, Is.Not.EqualTo(Guid.Empty));
        Assert.That(entry.StateType, Is.EqualTo(typeof(CancelledState)));
        Assert.That(entry.Priority, Is.EqualTo(LSESPriority.CRITICAL));
        Assert.That(entry.Handler, Is.Not.Null);
        Assert.That(entry.Condition, Is.Not.Null);
        Assert.That(entry.ExecutionCount, Is.EqualTo(0));
    }

    [Test]
    public void Test_LSESPriority_Enumeration_Values() {
        // Test that all expected priorities exist
        Assert.That(Enum.IsDefined(typeof(LSESPriority), LSESPriority.BACKGROUND));
        Assert.That(Enum.IsDefined(typeof(LSESPriority), LSESPriority.LOW));
        Assert.That(Enum.IsDefined(typeof(LSESPriority), LSESPriority.NORMAL));
        Assert.That(Enum.IsDefined(typeof(LSESPriority), LSESPriority.HIGH));
        Assert.That(Enum.IsDefined(typeof(LSESPriority), LSESPriority.CRITICAL));

        // Test priority ordering (lower numbers = higher priority)
        Assert.That((int)LSESPriority.BACKGROUND, Is.EqualTo(0));
        Assert.That((int)LSESPriority.LOW, Is.EqualTo(1));
        Assert.That((int)LSESPriority.NORMAL, Is.EqualTo(2));
        Assert.That((int)LSESPriority.HIGH, Is.EqualTo(3));
        Assert.That((int)LSESPriority.CRITICAL, Is.EqualTo(4));
    }

    #endregion

    #region Handler Registration Tests

    [Test]
    public void Test_PhaseHandlerRegister_Building_And_Registration() {
        // Arrange
        var register = PhaseHandlerRegister<BusinessState.ValidatePhaseState>.Create(_dispatcher);

        // Act
        var entry = register
            .WithPriority(LSESPriority.HIGH)
            .When((evt, handlerEntry) => evt is TestUserRegistrationEvent)
            .Handler(context => {
                return HandlerProcessResult.SUCCESS;
            })
            .Build();

        // Assert
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.PhaseType, Is.EqualTo(typeof(BusinessState.ValidatePhaseState)));
        Assert.That(entry.Priority, Is.EqualTo(LSESPriority.HIGH));
        Assert.That(entry.Handler, Is.Not.Null);
        Assert.That(entry.Condition, Is.Not.Null);
        Assert.That(register.IsBuild, Is.True);
    }

    [Test]
    public void Test_StateHandlerRegister_Building_And_Registration() {
        // Arrange
        var register = new StateHandlerRegister<CompletedState>(_dispatcher);

        // Act
        var entry = register
            .WithPriority(LSESPriority.LOW)
            .When((evt, handlerEntry) => evt.IsCompleted)
            .Handler(evt => {
                // Do something when event is completed
                Assert.That(evt.IsCompleted, Is.True);
            })
            .Build();

        // Assert
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry.StateType, Is.EqualTo(typeof(CompletedState)));
        Assert.That(entry.Priority, Is.EqualTo(LSESPriority.LOW));
        Assert.That(entry.Handler, Is.Not.Null);
        Assert.That(entry.Condition, Is.Not.Null);
        Assert.That(register.IsBuild, Is.True);
    }

    [Test]
    public void Test_EventSystemRegister_Multiple_Handlers() {
        // Arrange
        var register = new EventSystemRegister<TestUserRegistrationEvent>(_dispatcher);
        var executionOrder = new List<string>();

        // Act
        var handlerIds = register
            .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                phaseRegister
                    .WithPriority(LSESPriority.HIGH)
                    .Handler(context => {
                        executionOrder.Add("VALIDATE");
                        return HandlerProcessResult.SUCCESS;
                    })
                    .Build()
            )
            .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                phaseRegister
                    .WithPriority(LSESPriority.NORMAL)
                    .Handler(context => {
                        executionOrder.Add("EXECUTE");
                        return HandlerProcessResult.SUCCESS;
                    })
                    .Build()
            )
            .OnState<CompletedState>(stateRegister =>
                stateRegister
                    .WithPriority(LSESPriority.LOW)
                    .Handler(evt => {
                        executionOrder.Add("COMPLETED");
                    })
                    .Build()
            )
            .Register();

        // Assert
        Assert.That(handlerIds, Is.Not.Null);
        Assert.That(handlerIds.Length, Is.EqualTo(3));
        Assert.That(handlerIds.All(id => id != Guid.Empty), Is.True);

        // Verify handlers are registered
        var handlers = _dispatcher.getHandlers(typeof(TestUserRegistrationEvent));
        Assert.That(handlers.Count, Is.EqualTo(3));
    }

    #endregion

    #region Integration Tests

    [Test]
    public void Test_Complete_Event_Processing_Flow() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var executionOrder = new List<string>();

        // Register handlers for different phases
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.HIGH)
                        .Handler(context => {
                            executionOrder.Add("VALIDATE");
                            var evt = (TestUserRegistrationEvent)context.Event;
                            Assert.That(evt.Email, Is.EqualTo("test@example.com"));
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ConfigurePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.NORMAL)
                        .Handler(context => {
                            executionOrder.Add("CONFIGURE");
                            context.Event.SetData("configured", true);
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.NORMAL)
                        .Handler(context => {
                            executionOrder.Add("EXECUTE");
                            Assert.That(context.Event.GetData<bool>("configured"), Is.True);
                            context.Event.SetData("executed", true);
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnState<SucceedState>(stateRegister =>
                    stateRegister
                        .Handler(evt => {
                            executionOrder.Add("SUCCESS");
                        })
                        .Build()
                )
                .OnState<CompletedState>(stateRegister =>
                    stateRegister
                        .Handler(evt => {
                            executionOrder.Add("COMPLETED");
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(executionOrder.Count, Is.GreaterThan(0));
        Assert.That(executionOrder.Contains("VALIDATE"), Is.True);
        Assert.That(userEvent.GetData<bool>("configured"), Is.True);
        Assert.That(userEvent.GetData<bool>("executed"), Is.True);
    }

    [Test]
    public void Test_Priority_Based_Handler_Execution() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var executionOrder = new List<string>();

        // Register handlers with different priorities in the same phase
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.LOW)
                        .Handler(context => {
                            executionOrder.Add("LOW_PRIORITY");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.CRITICAL)
                        .Handler(context => {
                            executionOrder.Add("CRITICAL_PRIORITY");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .WithPriority(LSESPriority.NORMAL)
                        .Handler(context => {
                            executionOrder.Add("NORMAL_PRIORITY");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(executionOrder.Count, Is.EqualTo(3));

        // Should execute in priority order: CRITICAL (4) -> NORMAL (2) -> LOW (1)
        // Note: Higher priority values execute first in this implementation
        var criticalIndex = executionOrder.IndexOf("CRITICAL_PRIORITY");
        var normalIndex = executionOrder.IndexOf("NORMAL_PRIORITY");
        var lowIndex = executionOrder.IndexOf("LOW_PRIORITY");

        Assert.That(criticalIndex, Is.LessThan(normalIndex));
        Assert.That(normalIndex, Is.LessThan(lowIndex));
    }

    [Test]
    public void Test_Conditional_Handler_Execution() {
        // Arrange
        var userEvent1 = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var userEvent2 = new TestUserRegistrationEvent(_dispatcher, "admin@example.com", "Admin User", "admin");
        var webHandlerExecuted = false;
        var adminHandlerExecuted = false;
        int executionCount = 0;

        // Register conditional handlers
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .When((evt, entry) => {
                            var userEvt = evt as TestUserRegistrationEvent;
                            return userEvt?.RegistrationSource == "web";
                        })
                        .Handler(context => {
                            webHandlerExecuted = true;
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .When((evt, entry) => {
                            var userEvt = evt as TestUserRegistrationEvent;
                            return userEvt?.RegistrationSource == "admin";
                        })
                        .Handler(context => {
                            adminHandlerExecuted = true;
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .When((evt, entry) => entry.ExecutionCount == 0) //run only if no prior handler executed
                        .Handler(context => {
                            executionCount++;
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result1 = userEvent1.Dispatch();
        Assert.That(executionCount, Is.EqualTo(1)); // First event should trigger execution
        var result2 = userEvent2.Dispatch();
        Assert.That(executionCount, Is.EqualTo(1)); // Second event should not trigger execution again

        // Assert
        Assert.That(result1, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(result2, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(webHandlerExecuted, Is.True);
        Assert.That(adminHandlerExecuted, Is.True);
    }

    [Test]
    public void Test_Handler_Failure_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var executionOrder = new List<string>();

        // Register handlers with one that fails
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            executionOrder.Add("VALIDATE_SUCCESS");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            executionOrder.Add("EXECUTE_FAILURE");
                            return HandlerProcessResult.FAILURE;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.CleanupPhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            executionOrder.Add("CLEANUP");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(executionOrder.Contains("VALIDATE_SUCCESS"), Is.True);
        Assert.That(executionOrder.Contains("EXECUTE_FAILURE"), Is.True);
        // Should still reach cleanup phase even after failure
        Assert.That(executionOrder.Contains("CLEANUP"), Is.True);
    }

    [Test]
    public void Test_Handler_Cancellation_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var executionOrder = new List<string>();

        // Register handlers with one that cancels
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            executionOrder.Add("VALIDATE_CANCEL");
                            return HandlerProcessResult.CANCELLED;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            executionOrder.Add("EXECUTE_SHOULD_NOT_RUN");
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnState<CancelledState>(stateRegister =>
                    stateRegister
                        .Handler(evt => {
                            executionOrder.Add("CANCELLED_STATE");
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));
        Assert.That(executionOrder.Contains("VALIDATE_CANCEL"), Is.True);
        Assert.That(executionOrder.Contains("EXECUTE_SHOULD_NOT_RUN"), Is.False);
        Assert.That(userEvent.IsCancelled, Is.True);
    }

    [Test]
    public void Test_Event_Data_Persistence_Across_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        const string testKey = "cross.phase.data";
        const string testValue = "persistent-value";

        // Register handlers that set and verify data across phases
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ValidatePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            context.Event.SetData(testKey, testValue);
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            var retrievedValue = context.Event.GetData<string>(testKey);
                            Assert.That(retrievedValue, Is.EqualTo(testValue));
                            context.Event.SetData("execute.verified", true);
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .OnState<CompletedState>(stateRegister =>
                    stateRegister
                        .Handler(evt => {
                            var retrievedValue = evt.GetData<string>(testKey);
                            Assert.That(retrievedValue, Is.EqualTo(testValue));
                            var executeVerified = evt.GetData<bool>("execute.verified");
                            Assert.That(executeVerified, Is.True);
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(userEvent.GetData<string>(testKey), Is.EqualTo(testValue));
        Assert.That(userEvent.GetData<bool>("execute.verified"), Is.True);
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void Test_PhaseHandlerRegister_Invalid_Configuration() {
        // Arrange
        var register = PhaseHandlerRegister<BusinessState.ValidatePhaseState>.Create(_dispatcher);

        // Act & Assert - Building without handler should throw
        Assert.Throws<LSArgumentNullException>(() => register.Build());

        // Act & Assert - Registering without building should throw
        Assert.Throws<LSArgumentNullException>(() => register.Register());
    }

    [Test]
    public void Test_StateHandlerRegister_Invalid_Configuration() {
        // Arrange
        var register = new StateHandlerRegister<CompletedState>(_dispatcher);

        // Act & Assert - Building without handler should throw
        Assert.Throws<LSException>(() => register.Build());

        // Act & Assert - Building twice should throw
        register.Handler(evt => { });
        register.Build();
        Assert.Throws<LSException>(() => register.Build());
    }

    [Test]
    public void Test_EventSystemRegister_Null_Handler_Entry() {
        // Arrange
        var register = new EventSystemRegister<TestUserRegistrationEvent>(_dispatcher);

        // Act & Assert - Null phase handler should throw
        Assert.Throws<LSArgumentNullException>(() =>
            register.OnPhase<BusinessState.ValidatePhaseState>(phaseRegister => null!)
        );

        // Act & Assert - Null state handler should throw  
        Assert.Throws<LSArgumentNullException>(() =>
            register.OnState<CompletedState>(stateRegister => null!)
        );
    }

    [Test]
    public void Test_Dispatcher_ForEvent_Exception_Handling() {
        // Arrange
        // Act - Invalid configuration should return empty array
        var handlerIds = _dispatcher.ForEvent<TestUserRegistrationEvent>(register => {
            throw new InvalidOperationException("Test exception");
        });

        // Assert
        Assert.That(handlerIds, Is.Not.Null);
        Assert.That(handlerIds.Length, Is.EqualTo(0));
    }

    [Test]
    public void Test_Dispatcher_ForEventPhase_Exception_Handling() {
        // Act - Invalid configuration should return empty GUID
        var handlerId = _dispatcher.ForEventPhase<TestUserRegistrationEvent, BusinessState.ValidatePhaseState>(register => {
            throw new InvalidOperationException("Test exception");
        });

        // Assert
        Assert.That(handlerId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void Test_Dispatcher_ForEventState_Exception_Handling() {
        // Act - Invalid configuration should return empty GUID
        var handlerId = _dispatcher.ForEventState<TestUserRegistrationEvent, CompletedState>(register => {
            throw new InvalidOperationException("Test exception");
        });

        // Assert
        Assert.That(handlerId, Is.EqualTo(Guid.Empty));
    }

    #endregion

    #region State Specific Tests

    [Test]
    public void Test_CancelledState_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var handlers = new List<IHandlerEntry>();
        var context = EventSystemContext.Create(_dispatcher, userEvent, handlers);
        var cancelledState = new CancelledState(context);

        // Act
        var nextState = cancelledState.Process();

        // Assert
        Assert.That(nextState, Is.TypeOf<CompletedState>());
        Assert.That(cancelledState.Resume(), Is.Null);
        Assert.That(cancelledState.Cancel(), Is.Null);
        Assert.That(cancelledState.Fail(), Is.Null);
    }

    [Test]
    public void Test_CompletedState_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var handlers = new List<IHandlerEntry>();
        var context = EventSystemContext.Create(_dispatcher, userEvent, handlers);
        var completedState = new CompletedState(context);

        // Act
        var nextState = completedState.Process();

        // Assert
        Assert.That(nextState, Is.Null);
        Assert.That(completedState.Resume(), Is.Null);
        Assert.That(completedState.Cancel(), Is.Null);
        Assert.That(completedState.Fail(), Is.Null);
    }

    [Test]
    public void Test_SucceedState_Processing() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var handlers = new List<IHandlerEntry>();
        var context = EventSystemContext.Create(_dispatcher, userEvent, handlers);
        var succeedState = new SucceedState(context);

        // Act
        var nextState = succeedState.Process();

        // Assert
        Assert.That(nextState, Is.TypeOf<CompletedState>());
        Assert.That(succeedState.Resume(), Is.Null);
        Assert.That(succeedState.Cancel(), Is.Null);
        Assert.That(succeedState.Fail(), Is.Null);
    }

    #endregion

    #region Performance and Edge Case Tests

    [Test]
    public void Test_Multiple_Events_Parallel_Processing() {
        // Arrange
        var events = new List<TestUserRegistrationEvent>();
        var processedEmails = new List<string>();

        for (int i = 0; i < 10; i++) {
            events.Add(new TestUserRegistrationEvent(_dispatcher, $"user{i}@example.com", $"User {i}", "web"));
        }

        // Register handler
        _dispatcher.ForEvent<TestUserRegistrationEvent>(register =>
            register
                .OnPhase<BusinessState.ExecutePhaseState>(phaseRegister =>
                    phaseRegister
                        .Handler(context => {
                            var evt = (TestUserRegistrationEvent)context.Event;
                            lock (processedEmails) {
                                processedEmails.Add(evt.Email);
                            }
                            return HandlerProcessResult.SUCCESS;
                        })
                        .Build()
                )
                .Register()
        );

        // Act
        var results = events.Select(evt => evt.Dispatch()).ToList();

        // Assert
        Assert.That(results.All(r => r == EventProcessResult.SUCCESS), Is.True);
        Assert.That(processedEmails.Count, Is.EqualTo(10));
        Assert.That(processedEmails.All(email => email.Contains("@example.com")), Is.True);
    }

    [Test]
    public void Test_Event_With_No_Handlers() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");

        // Act - Process event with no registered handlers
        var result = userEvent.Dispatch();

        // Assert
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(userEvent.IsCompleted, Is.False); // No state changes occurred
    }

    [Test]
    public void Test_Large_Event_Data_Storage() {
        // Arrange
        var userEvent = new TestUserRegistrationEvent(_dispatcher, "test@example.com", "Test User", "web");
        var largeDataStructure = new Dictionary<string, object>();

        // Add large amount of data
        for (int i = 0; i < 1000; i++) {
            largeDataStructure[$"key_{i}"] = $"value_{i}_{Guid.NewGuid()}";
        }

        // Act
        userEvent.SetData("large.dataset", largeDataStructure);
        var retrievedData = userEvent.GetData<Dictionary<string, object>>("large.dataset");

        // Assert
        Assert.That(retrievedData, Is.Not.Null);
        Assert.That(retrievedData.Count, Is.EqualTo(1000));
        Assert.That(retrievedData["key_500"], Is.Not.Null);
    }
    #endregion
}
