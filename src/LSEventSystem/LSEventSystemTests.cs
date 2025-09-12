using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.EventSystem;

namespace LSUtils.EventSystem.Tests;

/// <summary>
/// Comprehensive NUnit tests for LSEventSystem.
/// </summary>
[TestFixture]
public class LSEventSystemTests {
    // user registration class
    public class User : ILSEventable {
        public string Email { get; set; }
        public string Password { get; set; }

        public LSDispatcher? Dispatcher => throw new NotImplementedException();

        //constructor
        public User(string email, string password) {
            Email = email;
            Password = password;
        }

        public EventProcessResult Initialize(LSEventOptions options) {
            return OnInitializeEvent.Create(this, options).Dispatch();
        }
    }

    //user registration event class LSEvent<TInstance>
    public class UserRegistrationEvent : LSEvent<User> {
        public UserRegistrationEvent(User instance, LSEventOptions? options) : base(instance, options) { }
    }
    //setup
    private LSDispatcher? _dispatcher = new LSDispatcher();
    [SetUp]
    public void Setup() {
        _dispatcher = new LSDispatcher();
    }

    //cleanup
    [TearDown]
    public void Cleanup() {
        _dispatcher = null;
    }

    //Test user creation
    [Test]
    public void TestUserCreation() {
        var user = new User("test@example.com", "password123");
        Assert.That(user.Email, Is.EqualTo("test@example.com"));
        Assert.That(user.Password, Is.EqualTo("password123"));
    }
    //Test user initialization
    [Test]
    public void TestUserInitialization() {
        var user = new User("test@example.com", "password123");
        var result = user.Initialize(new LSEventOptions(_dispatcher));
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
    }
    //Test user registration event
    [Test]
    public void TestUserRegistrationEvent() {
        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
    }
    //Test user registration event with global handler
    [Test]
    public void TestUserRegistrationForEventWithGlobalPhaseHandler() {
        List<string> handledPhases = new List<string>();
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("ValidatePhase handler executed.");
                handledPhases.Add("ValidatePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("ConfigurePhase handler executed.");
                handledPhases.Add("ConfigurePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("ExecutePhase handler executed.");
                handledPhases.Add("ExecutePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            //cleanup
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("CleanupPhase handler executed.");
                handledPhases.Add("CleanupPhase");
                return HandlerProcessResult.SUCCESS;
            })));
        var user = new User("test@example.com", "password123");
        Console.WriteLine("User created.");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();
        Console.WriteLine("User registration event dispatched.");
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(handledPhases, Is.EquivalentTo(new[] { "ValidatePhase", "ConfigurePhase", "ExecutePhase", "CleanupPhase" }));

        var anotherUserRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var anotherResult = anotherUserRegistrationEvent.Dispatch();
        Console.WriteLine("Another user registration event dispatched.");
        Assert.That(anotherResult, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(handledPhases, Is.EquivalentTo(new[] { "ValidatePhase", "ConfigurePhase", "ExecutePhase", "CleanupPhase", "ValidatePhase", "ConfigurePhase", "ExecutePhase", "CleanupPhase" }));
        Console.WriteLine("Test completed");
    }
    //Test user registration event with event-scoped handler
    [Test]
    public void TestUserRegistrationForEventWithEventScopedHandler() {
        List<string> handledPhases = new List<string>();
        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent
            .WithCallback<UserRegistrationEvent>(
                evtRegister => evtRegister
                .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                    Console.WriteLine("ValidatePhase handler executed.");
                    handledPhases.Add("ValidatePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ConfigurePhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("ConfigurePhase handler executed.");
                    handledPhases.Add("ConfigurePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ExecutePhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("ExecutePhase handler executed.");
                    handledPhases.Add("ExecutePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.CleanupPhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("CleanupPhase handler executed.");
                    handledPhases.Add("CleanupPhase");
                    return HandlerProcessResult.SUCCESS;
                }))
            )
            .Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(handledPhases, Is.EquivalentTo(new[] { "ValidatePhase", "ConfigurePhase", "ExecutePhase", "CleanupPhase" }));
        Console.WriteLine("Test completed");
    }

    //Test user registration event with both global and event-scoped handlers
    [Test]
    public void TestUserRegistrationWithGlobalAndEventScopedHandlers() {
        List<string> handledPhases = new List<string>();

        // Register global handlers first
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("GLOBAL ValidatePhase handler executed.");
                handledPhases.Add("GLOBAL_ValidatePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("GLOBAL ConfigurePhase handler executed.");
                handledPhases.Add("GLOBAL_ConfigurePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("GLOBAL ExecutePhase handler executed.");
                handledPhases.Add("GLOBAL_ExecutePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("GLOBAL CleanupPhase handler executed.");
                handledPhases.Add("GLOBAL_CleanupPhase");
                return HandlerProcessResult.SUCCESS;
            })));

        var user = new User("test@example.com", "password123");
        Console.WriteLine("User created.");

        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Add event-scoped handlers and dispatch
        var result = userRegistrationEvent
            .WithCallback<UserRegistrationEvent>(
                evtRegister => evtRegister
                .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                    Console.WriteLine("EVENT-SCOPED ValidatePhase handler executed.");
                    handledPhases.Add("EVENT_ValidatePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ConfigurePhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("EVENT-SCOPED ConfigurePhase handler executed.");
                    handledPhases.Add("EVENT_ConfigurePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ExecutePhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("EVENT-SCOPED ExecutePhase handler executed.");
                    handledPhases.Add("EVENT_ExecutePhase");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.CleanupPhaseState>(pReg => pReg.Handler(ctx => {
                    Console.WriteLine("EVENT-SCOPED CleanupPhase handler executed.");
                    handledPhases.Add("EVENT_CleanupPhase");
                    return HandlerProcessResult.SUCCESS;
                }))
            )
            .Dispatch();

        Console.WriteLine("User registration event with both global and event-scoped handlers dispatched.");

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));

        // Check that both global and event-scoped handlers were executed
        // The exact order depends on your LSEventSystem implementation
        Assert.That(handledPhases.Count, Is.EqualTo(8), "Expected 8 handlers to be executed (4 global + 4 event-scoped)");

        // Verify that all expected handler types were called
        Assert.That(handledPhases.Count(h => h.StartsWith("GLOBAL_")), Is.EqualTo(4), "Expected 4 global handlers");
        Assert.That(handledPhases.Count(h => h.StartsWith("EVENT_")), Is.EqualTo(4), "Expected 4 event-scoped handlers");

        // Verify that all phases were handled
        Assert.That(handledPhases.Any(h => h.Contains("ValidatePhase")), Is.True, "ValidatePhase should be handled");
        Assert.That(handledPhases.Any(h => h.Contains("ConfigurePhase")), Is.True, "ConfigurePhase should be handled");
        Assert.That(handledPhases.Any(h => h.Contains("ExecutePhase")), Is.True, "ExecutePhase should be handled");
        Assert.That(handledPhases.Any(h => h.Contains("CleanupPhase")), Is.True, "CleanupPhase should be handled");

        Console.WriteLine($"Handlers executed in order: {string.Join(", ", handledPhases)}");
        Console.WriteLine("Test completed - both global and event-scoped handlers worked together");
    }

    //Test BusinessState processing through all phases
    [Test]
    public void TestBusinessStateProcessing() {
        List<string> handledPhases = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("BusinessState - ValidatePhase executed.");
                handledPhases.Add("Validate");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("BusinessState - ConfigurePhase executed.");
                handledPhases.Add("Configure");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("BusinessState - ExecutePhase executed.");
                handledPhases.Add("Execute");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(pReg => pReg.Handler(ctx => {
                Console.WriteLine("BusinessState - CleanupPhase executed.");
                handledPhases.Add("Cleanup");
                return HandlerProcessResult.SUCCESS;
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(handledPhases, Is.EqualTo(new[] { "Validate", "Configure", "Execute", "Cleanup" }));
        Console.WriteLine("BusinessState test completed - all phases executed in correct order");
    }

    //Test SucceedState behavior after successful business processing
    [Test]
    public void TestSucceedStateHandlers() {
        List<string> executedHandlers = new List<string>();

        // Register success state handlers
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                Console.WriteLine("SucceedState handler executed.");
                executedHandlers.Add("Success");
            }))
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("Business phase handler executed.");
                executedHandlers.Add("BusinessPhase");
                return HandlerProcessResult.SUCCESS;
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(executedHandlers, Contains.Item("BusinessPhase"));
        Assert.That(executedHandlers, Contains.Item("Success"));
        Console.WriteLine("SucceedState test completed - success handlers executed after business phases");
    }

    //Test CompletedState behavior as final terminal state
    [Test]
    public void TestCompletedStateHandlers() {
        List<string> executedHandlers = new List<string>();

        // Register completion state handlers
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                Console.WriteLine("CompletedState handler executed.");
                executedHandlers.Add("Completed");
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                Console.WriteLine("SucceedState handler executed.");
                executedHandlers.Add("Success");
            }))
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("Business phase handler executed.");
                executedHandlers.Add("BusinessPhase");
                return HandlerProcessResult.SUCCESS;
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(executedHandlers, Contains.Item("BusinessPhase"));
        Assert.That(executedHandlers, Contains.Item("Success"));
        Assert.That(executedHandlers, Contains.Item("Completed"));
        Console.WriteLine("CompletedState test completed - completion handlers executed as final step");
    }

    //Test CancelledState behavior when event is cancelled
    [Test]
    public void TestCancelledStateHandlers() {
        List<string> executedHandlers = new List<string>();

        // Register cancellation state handlers
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                Console.WriteLine("CancelledState handler executed.");
                executedHandlers.Add("Cancelled");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                Console.WriteLine("CompletedState handler executed after cancellation.");
                executedHandlers.Add("Completed");
            }))
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                Console.WriteLine("Business phase handler - simulating cancellation.");
                executedHandlers.Add("BusinessPhase");
                return HandlerProcessResult.CANCELLED;
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));
        Assert.That(executedHandlers, Contains.Item("BusinessPhase"));
        Assert.That(executedHandlers, Contains.Item("Cancelled"));
        Assert.That(executedHandlers, Contains.Item("Completed"));
        Console.WriteLine("CancelledState test completed - cancellation flow executed correctly");
    }

    //Test state transitions flow (Business → Success → Completed)
    [Test]
    public void TestStateTransitionFlow() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                Console.WriteLine("BusinessState-Validate executed");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Execute");
                Console.WriteLine("BusinessState-Execute executed");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
                Console.WriteLine("SucceedState executed");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
                Console.WriteLine("CompletedState executed");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));

        // Print the full flow for debugging
        Console.WriteLine($"Full state flow: {string.Join(" → ", stateFlow)}");

        // Verify state transition order (adjust based on actual implementation)
        var businessStateIndex = stateFlow.FindIndex(s => s.StartsWith("BusinessState"));
        var succeedStateIndex = stateFlow.FindIndex(s => s == "SucceedState");
        var completedStateIndex = stateFlow.FindIndex(s => s == "CompletedState");

        Console.WriteLine($"BusinessState index: {businessStateIndex}, SucceedState index: {succeedStateIndex}, CompletedState index: {completedStateIndex}");

        // Verify that BusinessState executes before other states
        Assert.That(businessStateIndex, Is.GreaterThanOrEqualTo(0), "BusinessState should execute");

        // Verify both SucceedState and CompletedState execute
        Assert.That(succeedStateIndex, Is.GreaterThanOrEqualTo(0), "SucceedState should execute");
        Assert.That(completedStateIndex, Is.GreaterThanOrEqualTo(0), "CompletedState should execute");

        // BusinessState should execute before both terminal states
        if (succeedStateIndex >= 0) {
            Assert.That(businessStateIndex, Is.LessThan(succeedStateIndex), "BusinessState should execute before SucceedState");
        }
        if (completedStateIndex >= 0) {
            Assert.That(businessStateIndex, Is.LessThan(completedStateIndex), "BusinessState should execute before CompletedState");
        }

        Console.WriteLine($"State transition flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("State transition test completed - correct flow verified");
    }

    //Test failure state transitions (Business → Completed, skipping Success)
    [Test]
    public void TestFailureStateTransitionFlow() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.FAILURE;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.FAILURE));

        // Verify that SucceedState was skipped due to failure
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped on failure");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Cleanup"), "Cleanup should be skipped when ValidatePhase fails");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should still execute");

        Console.WriteLine($"Failure state flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("Failure state transition test completed - ValidatePhase failure skips all remaining phases");
    }

    //Test cancellation state transitions (Business → Cancelled → Completed)
    [Test]
    public void TestCancellationStateTransitionFlow() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.CANCELLED;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify cancellation flow
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped on cancellation");
        Assert.That(stateFlow, Contains.Item("CancelledState"), "CancelledState should execute");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute after cancellation");

        var cancelledIndex = stateFlow.FindIndex(s => s == "CancelledState");
        var completedIndex = stateFlow.FindIndex(s => s == "CompletedState");
        Assert.That(cancelledIndex, Is.LessThan(completedIndex), "CancelledState should execute before CompletedState");

        Console.WriteLine($"Cancellation state flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("Cancellation state transition test completed - correct flow verified");
    }

    //Test cancellation from ValidatePhase - should go directly to CancelledState
    [Test]
    public void TestValidatePhaseCancellation() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.CANCELLED;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Configure");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Execute");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify ValidatePhase cancellation skips all remaining phases
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Configure"), "ConfigurePhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Execute"), "ExecutePhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Cleanup"), "CleanupPhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped");
        Assert.That(stateFlow, Contains.Item("CancelledState"), "CancelledState should execute");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        Console.WriteLine($"ValidatePhase cancellation flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("ValidatePhase cancellation test completed - direct transition to CancelledState");
    }

    //Test cancellation from ConfigurePhase - should go directly to CancelledState  
    [Test]
    public void TestConfigurePhaseCancellation() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Configure");
                return HandlerProcessResult.CANCELLED;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Execute");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify ConfigurePhase cancellation skips remaining phases
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Configure"), "ConfigurePhase should execute");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Execute"), "ExecutePhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Cleanup"), "CleanupPhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped");
        Assert.That(stateFlow, Contains.Item("CancelledState"), "CancelledState should execute");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        Console.WriteLine($"ConfigurePhase cancellation flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("ConfigurePhase cancellation test completed - direct transition to CancelledState");
    }

    //Test cancellation from ExecutePhase - should go directly to CancelledState
    [Test]
    public void TestExecutePhaseCancellation() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Configure");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Execute");
                return HandlerProcessResult.CANCELLED;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify ExecutePhase cancellation skips remaining phases
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Configure"), "ConfigurePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Execute"), "ExecutePhase should execute");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Cleanup"), "CleanupPhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped");
        Assert.That(stateFlow, Contains.Item("CancelledState"), "CancelledState should execute");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        Console.WriteLine($"ExecutePhase cancellation flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("ExecutePhase cancellation test completed - direct transition to CancelledState");
    }

    //Test cancellation from CleanupPhase - should go directly to CancelledState
    [Test]
    public void TestCleanupPhaseCancellation() {
        List<string> stateFlow = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Validate");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Configure");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Execute");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                stateFlow.Add("BusinessState-Cleanup");
                return HandlerProcessResult.CANCELLED;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                stateFlow.Add("SucceedState");
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS)); // CleanupPhase cancellation still results in SUCCESS

        // Verify CleanupPhase cancellation goes through SucceedState → CompletedState (core phases completed)
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Configure"), "ConfigurePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Execute"), "ExecutePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Cleanup"), "CleanupPhase should execute");
        Assert.That(stateFlow, Contains.Item("SucceedState"), "SucceedState should execute (core phases completed)");
        Assert.That(stateFlow, Does.Not.Contain("CancelledState"), "CancelledState should NOT execute (only cleanup cancelled)");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        // Verify execution order: SucceedState → CompletedState (no CancelledState)
        var succeedIndex = stateFlow.FindIndex(s => s == "SucceedState");
        var completedIndex = stateFlow.FindIndex(s => s == "CompletedState");
        Assert.That(succeedIndex, Is.LessThan(completedIndex), "SucceedState should execute before CompletedState");

        Console.WriteLine($"CleanupPhase cancellation flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("CleanupPhase cancellation test completed - core phases succeeded, cleanup cancelled but still goes to SucceedState");
    }

    //Test multiple events with cancellation in different phases
    [Test]
    public void TestMultipleEventsCancellationFlow() {
        List<string> allStateFlows = new List<string>();

        // Test cancellation in each phase with separate events
        var phases = new[] {
            ("Validate", typeof(LSEventBusinessState.ValidatePhaseState)),
            ("Configure", typeof(LSEventBusinessState.ConfigurePhaseState)),
            ("Execute", typeof(LSEventBusinessState.ExecutePhaseState)),
            ("Cleanup", typeof(LSEventBusinessState.CleanupPhaseState))
        };

        foreach (var (phaseName, phaseType) in phases) {
            List<string> stateFlow = new List<string>();

            // Create a new dispatcher for each test to avoid interference
            var localDispatcher = new LSDispatcher();

            // Register handlers for all phases, but make the target phase return CANCELLED
            localDispatcher.ForEvent<UserRegistrationEvent>(registration => {
                if (phaseType == typeof(LSEventBusinessState.ValidatePhaseState)) {
                    registration.OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}");
                        return HandlerProcessResult.CANCELLED;
                    }));
                } else {
                    registration.OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Validate");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.ConfigurePhaseState)) {
                    registration.OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}");
                        return HandlerProcessResult.CANCELLED;
                    }));
                } else {
                    registration.OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Configure");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.ExecutePhaseState)) {
                    registration.OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}");
                        return HandlerProcessResult.CANCELLED;
                    }));
                } else {
                    registration.OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Execute");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.CleanupPhaseState)) {
                    registration.OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}");
                        return HandlerProcessResult.CANCELLED;
                    }));
                } else {
                    registration.OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Cleanup");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                // Register state handlers
                return registration
                    .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                        stateFlow.Add("SucceedState");
                    }))
                    .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                        stateFlow.Add("CancelledState");
                    }))
                    .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                        stateFlow.Add("CompletedState");
                    }));
            });

            // Execute the event
            var user = new User("test@example.com", "password123");
            var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(localDispatcher));
            var result = userRegistrationEvent.Dispatch();

            // Verify cancellation result
            if (phaseName == "Cleanup") {
                // CleanupPhase cancellation should result in SUCCESS (core phases completed)
                Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS), $"{phaseName}Phase cancellation should return SUCCESS (core phases completed)");
                Assert.That(stateFlow, Does.Not.Contain("CancelledState"), $"{phaseName}Phase cancellation should NOT execute CancelledState");
                Assert.That(stateFlow, Contains.Item("SucceedState"), $"{phaseName}Phase cancellation should execute SucceedState (core phases completed)");
            } else {
                // Other phases should result in CANCELLED
                Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED), $"{phaseName}Phase cancellation should return CANCELLED");
                Assert.That(stateFlow, Contains.Item("CancelledState"), $"{phaseName}Phase cancellation should execute CancelledState");
                Assert.That(stateFlow, Does.Not.Contain("SucceedState"), $"{phaseName}Phase cancellation should skip SucceedState");
            }
            Assert.That(stateFlow, Contains.Item("CompletedState"), $"{phaseName}Phase cancellation should execute CompletedState");

            allStateFlows.Add($"{phaseName}: {string.Join(" → ", stateFlow)}");
            Console.WriteLine($"{phaseName}Phase cancellation: {string.Join(" → ", stateFlow)}");
        }

        Console.WriteLine("Multiple events cancellation test completed:");
        foreach (var flow in allStateFlows) {
            Console.WriteLine($"  {flow}");
        }
    }

    //Test cancellation from ValidatePhase with event-scoped handlers
    [Test]
    public void TestValidatePhaseCancellationEventScoped() {
        List<string> stateFlow = new List<string>();

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent
            .WithCallback<UserRegistrationEvent>(
                evtRegister => evtRegister
                .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Validate");
                    return HandlerProcessResult.CANCELLED;
                }))
                .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Configure");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Execute");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Cleanup");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                    stateFlow.Add("SucceedState");
                }))
                .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                    stateFlow.Add("CancelledState");
                }))
                .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                    stateFlow.Add("CompletedState");
                }))
            )
            .Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify ValidatePhase cancellation skips all remaining phases (event-scoped)
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Configure"), "ConfigurePhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Execute"), "ExecutePhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("BusinessState-Cleanup"), "CleanupPhase should be skipped");
        Assert.That(stateFlow, Does.Not.Contain("SucceedState"), "SucceedState should be skipped");
        Assert.That(stateFlow, Contains.Item("CancelledState"), "CancelledState should execute");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        Console.WriteLine($"ValidatePhase cancellation flow (event-scoped): {string.Join(" → ", stateFlow)}");
        Console.WriteLine("ValidatePhase cancellation test (event-scoped) completed - direct transition to CancelledState");
    }

    //Test cancellation from CleanupPhase with event-scoped handlers
    [Test]
    public void TestCleanupPhaseCancellationEventScoped() {
        List<string> stateFlow = new List<string>();

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent
            .WithCallback<UserRegistrationEvent>(
                evtRegister => evtRegister
                .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Validate");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Configure");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Execute");
                    return HandlerProcessResult.SUCCESS;
                }))
                .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                    stateFlow.Add("BusinessState-Cleanup");
                    return HandlerProcessResult.CANCELLED;
                }))
                .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                    stateFlow.Add("SucceedState");
                }))
                .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                    stateFlow.Add("CancelledState");
                }))
                .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                    stateFlow.Add("CompletedState");
                }))
            )
            .Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS)); // CleanupPhase cancellation still results in SUCCESS

        // Verify CleanupPhase cancellation goes through SucceedState → CompletedState (event-scoped)
        Assert.That(stateFlow, Contains.Item("BusinessState-Validate"), "ValidatePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Configure"), "ConfigurePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Execute"), "ExecutePhase should execute");
        Assert.That(stateFlow, Contains.Item("BusinessState-Cleanup"), "CleanupPhase should execute");
        Assert.That(stateFlow, Contains.Item("SucceedState"), "SucceedState should execute (core phases completed)");
        Assert.That(stateFlow, Does.Not.Contain("CancelledState"), "CancelledState should NOT execute (only cleanup cancelled)");
        Assert.That(stateFlow, Contains.Item("CompletedState"), "CompletedState should execute");

        // Verify execution order: SucceedState → CompletedState (no CancelledState)
        var succeedIndex = stateFlow.FindIndex(s => s == "SucceedState");
        var completedIndex = stateFlow.FindIndex(s => s == "CompletedState");
        Assert.That(succeedIndex, Is.LessThan(completedIndex), "SucceedState should execute before CompletedState");

        Console.WriteLine($"CleanupPhase cancellation flow (event-scoped): {string.Join(" → ", stateFlow)}");
        Console.WriteLine("CleanupPhase cancellation test (event-scoped) completed - core phases succeeded, cleanup cancelled but still goes to SucceedState");
    }

    //Test mixed global and event-scoped cancellation handlers
    [Test]
    public void TestMixedGlobalAndEventScopedCancellation() {
        List<string> stateFlow = new List<string>();

        // Register global handlers
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                stateFlow.Add("Global-ValidatePhase");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                stateFlow.Add("Global-CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                stateFlow.Add("Global-CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent
            .WithCallback<UserRegistrationEvent>(
                evtRegister => evtRegister
                .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                    stateFlow.Add("EventScoped-ConfigurePhase");
                    return HandlerProcessResult.CANCELLED; // Cancel in ConfigurePhase
                }))
                .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                    stateFlow.Add("EventScoped-CancelledState");
                }))
                .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                    stateFlow.Add("EventScoped-CompletedState");
                }))
            )
            .Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.CANCELLED));

        // Verify both global and event-scoped handlers executed
        Assert.That(stateFlow, Contains.Item("Global-ValidatePhase"), "Global ValidatePhase handler should execute");
        Assert.That(stateFlow, Contains.Item("EventScoped-ConfigurePhase"), "Event-scoped ConfigurePhase handler should execute");
        Assert.That(stateFlow, Contains.Item("Global-CancelledState"), "Global CancelledState handler should execute");
        Assert.That(stateFlow, Contains.Item("EventScoped-CancelledState"), "Event-scoped CancelledState handler should execute");
        Assert.That(stateFlow, Contains.Item("Global-CompletedState"), "Global CompletedState handler should execute");
        Assert.That(stateFlow, Contains.Item("EventScoped-CompletedState"), "Event-scoped CompletedState handler should execute");

        Console.WriteLine($"Mixed global/event-scoped cancellation flow: {string.Join(" → ", stateFlow)}");
        Console.WriteLine("Mixed global and event-scoped cancellation test completed - both handler types executed");
    }

    //Test WAITING state behavior - handlers requiring external input
    [Test]
    public void TestWaitingValidateFailure() {
        List<string> executedHandlers = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                executedHandlers.Add("ValidatePhase-Start");
                // Simulate handler that needs external input (e.g., email verification)
                return HandlerProcessResult.WAITING;
            }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                executedHandlers.Add("ConfigurePhase should not run");
                return HandlerProcessResult.SUCCESS;
            }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                executedHandlers.Add("SucceedState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                executedHandlers.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        // Verify that event is in FAILURE state
        Assert.That(result, Is.EqualTo(EventProcessResult.FAILURE));

        // Verify that only ValidatePhase started and other phases didn't execute
        Assert.That(executedHandlers, Contains.Item("ValidatePhase-Start"));
        Assert.That(executedHandlers, Does.Not.Contain("ConfigurePhase should not run")); // the event was aborted because Validate phase is waiting
        Assert.That(executedHandlers, Does.Not.Contain("SucceedState")); //the event was not successful
        Assert.That(executedHandlers, Does.Contain("CompletedState")); //complete always run

        Console.WriteLine($"TEST flow: {string.Join(" → ", executedHandlers)}");
        Console.WriteLine("TEST completed - event failed due to waiting handler in ValidatePhase");
    }


    /**
        THIS WHOLE TEST DOES NOT MAKE SENSE, I WAS IN THE MIDDLE TO TRY TO MAKE IT WORK
        BUT EACH PHASE HANDLES FAILURE DIFFERENTLY, SO IT'S NOT POSSIBLE TO HAVE A UNIFIED TEST LIKE THIS
    //Test FAILURE in different phases
    [Test]
    public void TestFailureInDifferentPhases() {

        /**
        // THIS IS THE WRONG WAY TO MAKE THIS TEST
        // its overcomplicated and hard to read

        var phases = new[] {
            ("Validate", typeof(LSEventBusinessState.ValidatePhaseState)),
            ("Configure", typeof(LSEventBusinessState.ConfigurePhaseState)),
            ("Execute", typeof(LSEventBusinessState.ExecutePhaseState)),
            ("Cleanup", typeof(LSEventBusinessState.CleanupPhaseState))
        };
        foreach (var (phaseName, phaseType) in phases) {
            List<string> stateFlow = new List<string>();
            var localDispatcher = new LSDispatcher();

            //no need to create a new dispatcher each time - just use event-scoped handlers
            localDispatcher.ForEvent<UserRegistrationEvent>(registration => {
                var reg = registration;
                //I don't understand why this is necessary, why not just condition the return HandlerProcessResult to either SUCCESS or FAILURE based on phaseName?
                if (phaseType == typeof(LSEventBusinessState.ValidatePhaseState)) {
                    reg = reg.OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}-FAILURE");
                        return HandlerProcessResult.FAILURE;
                    }));
                } else {
                    reg = reg.OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Validate");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.ConfigurePhaseState)) {
                    reg = reg.OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}-FAILURE");
                        return HandlerProcessResult.FAILURE;
                    }));
                } else {
                    reg = reg.OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Configure");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.ExecutePhaseState)) {
                    reg = reg.OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}-FAILURE");
                        return HandlerProcessResult.FAILURE;
                    }));
                } else {
                    reg = reg.OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Execute");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                if (phaseType == typeof(LSEventBusinessState.CleanupPhaseState)) {
                    reg = reg.OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                        stateFlow.Add($"BusinessState-{phaseName}-FAILURE");
                        return HandlerProcessResult.FAILURE;
                    }));
                } else {
                    reg = reg.OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg.Handler(ctx => {
                        stateFlow.Add("BusinessState-Cleanup");
                        return HandlerProcessResult.SUCCESS;
                    }));
                }

                return reg
                    .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                        stateFlow.Add("SucceedState");
                    }))
                    .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                        stateFlow.Add("CompletedState");
                    }));
            });

        var phases = new[] {
            ("Validate", typeof(LSEventBusinessState.ValidatePhaseState)),
            ("Configure", typeof(LSEventBusinessState.ConfigurePhaseState)),
            ("Execute", typeof(LSEventBusinessState.ExecutePhaseState)),
            ("Cleanup", typeof(LSEventBusinessState.CleanupPhaseState))
        };
        foreach (var (phaseName, phaseType) in phases) {
            List<string> stateFlow = new List<string>();
            var user = new User("test@example.com", "password123");
            var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
            var result = userRegistrationEvent
                .WithCallback<UserRegistrationEvent>(
                    reg => reg
                    .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg
                        .Handler(ctx => {
                            (HandlerProcessResult handlerResult, string status) = (phaseName == "Validate") ? (HandlerProcessResult.FAILURE, "FAILURE") : (HandlerProcessResult.SUCCESS, "SUCCESS");
                            stateFlow.Add($"BusinessState-{phaseName}-{status}");
                            return handlerResult;
                        }))
                    .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg
                        .Handler(ctx => {
                            (HandlerProcessResult handlerResult, string status) = (phaseName == "Configure") ? (HandlerProcessResult.FAILURE, "FAILURE") : (HandlerProcessResult.SUCCESS, "SUCCESS");
                            stateFlow.Add($"BusinessState-{phaseName}-{status}");
                            return handlerResult;
                        }))
                    .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg
                        .Handler(ctx => {
                            (HandlerProcessResult handlerResult, string status) = (phaseName == "Execute") ? (HandlerProcessResult.FAILURE, "FAILURE") : (HandlerProcessResult.SUCCESS, "SUCCESS");
                            stateFlow.Add($"BusinessState-{phaseName}-{status}");
                            return handlerResult;
                        }))
                    .OnPhase<LSEventBusinessState.CleanupPhaseState>(clReg => clReg
                        .Handler(ctx => {
                            (HandlerProcessResult handlerResult, string status) = (phaseName == "Cleanup") ? (HandlerProcessResult.FAILURE, "FAILURE") : (HandlerProcessResult.SUCCESS, "SUCCESS");
                            stateFlow.Add($"BusinessState-{phaseName}-{status}");
                            return handlerResult;
                        }))
                    .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                        stateFlow.Add("SucceedState");
                    }))
                    .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                        stateFlow.Add("CompletedState");
                    }))
                )
            .Dispatch();

            // All failures should result in FAILURE, but continue processing
            Assert.That(result, Is.EqualTo(EventProcessResult.FAILURE), $"{phaseName}Phase failure should return FAILURE");

            // Verify the failing phase executed
            Assert.That(stateFlow.Any(s => s.Contains($"{phaseName}-FAILURE")), Is.True, $"{phaseName}Phase failure handler should execute");

            // Verify SucceedState was skipped due to failure
            Assert.That(stateFlow, Does.Not.Contain("SucceedState"), $"{phaseName}Phase failure should skip SucceedState");

            // Verify CompletedState always executes
            Assert.That(stateFlow, Contains.Item("CompletedState"), $"{phaseName}Phase failure should execute CompletedState");

            Console.WriteLine($"{phaseName}Phase failure: {string.Join(" → ", stateFlow)}");


            Console.WriteLine("Failure in different phases test completed - all phases handle failures correctly");
        }
    }
    **/


    /** WHY CREATE THIS TEST IF THERE IS ONE THAT DOES THE EXACT SAME THING?
    TestUserRegistrationEvent
    //Test empty event (no handlers registered)
    [Test]
    public void TestEmptyEventNoHandlers() {
        // Don't register any handlers - test default behavior
        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        // Should complete successfully even with no handlers
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Console.WriteLine("Empty event test completed - no handlers, successful completion");
    }
    **/

    /** NOT GOING TO TRY FIX THIS, THIS WILL NEVER WORK, HANDLERS ARE OVERWRITED WHEN USED IN THE CHAIN
    //Test mixed SUCCESS/FAILURE results in same phase
    [Test]
    public void TestMixedSuccessFailureInSamePhase() {
        List<string> executedHandlers = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg
                .Handler(ctx => {
                    executedHandlers.Add("Handler1-SUCCESS");
                    return HandlerProcessResult.SUCCESS;
                })
                .Handler(ctx => {
                    executedHandlers.Add("Handler2-FAILURE");
                    return HandlerProcessResult.FAILURE;
                })
                .Handler(ctx => {
                    executedHandlers.Add("Handler3-SUCCESS");
                    return HandlerProcessResult.SUCCESS;
                })
                .Handler(ctx => {
                    executedHandlers.Add("Handler4-FAILURE");
                    return HandlerProcessResult.FAILURE;
                }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                executedHandlers.Add("SucceedState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                executedHandlers.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        var userRegistrationEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));
        var result = userRegistrationEvent.Dispatch();

        Assert.That(result, Is.EqualTo(EventProcessResult.FAILURE));

        // Verify all handlers executed regardless of individual results
        Assert.That(executedHandlers, Contains.Item("Handler1-SUCCESS"));
        Assert.That(executedHandlers, Contains.Item("Handler2-FAILURE"));
        Assert.That(executedHandlers, Contains.Item("Handler3-SUCCESS"));
        Assert.That(executedHandlers, Contains.Item("Handler4-FAILURE"));

        // Verify SucceedState skipped due to failures, but CompletedState executed
        Assert.That(executedHandlers, Does.Not.Contain("SucceedState"));
        Assert.That(executedHandlers, Contains.Item("CompletedState"));

        Console.WriteLine($"Mixed success/failure flow: {string.Join(" → ", executedHandlers)}");
        Console.WriteLine("Mixed success/failure test completed - all handlers executed despite mixed results");
    }
    **/

    #region Comprehensive WAITING Behavior Tests

    //Test ValidatePhase WAITING behavior - causes failure after all handlers complete
    [Test]
    public void TestValidatePhaseWaitingCausesFailure() {
        List<string> handlerResults = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg
                .Handler(ctx => {
                    handlerResults.Add("Handler1_Waiting");
                    return HandlerProcessResult.WAITING;
                })
                .Handler(ctx => {
                    handlerResults.Add("Handler2_Success");
                    return HandlerProcessResult.SUCCESS;
                })));

        var user = new User("test@example.com", "password123");
        var userEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        var result = userEvent.Dispatch();

        // Validate phase should fail if any handlers are waiting after all complete
        Assert.That(result, Is.EqualTo(EventProcessResult.FAILURE));
        Assert.That(handlerResults, Is.EqualTo(new[] { "Handler1_Waiting", "Handler2_Success" }));
        Console.WriteLine("ValidatePhase WAITING test completed - waiting causes failure");
    }

    //Test ConfigurePhase sequential waiting - resume before handler completes (pseudo-sequential)
    [Test]
    public void TestConfigurePhaseSequentialWaiting_PseudoSequential() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        EventProcessResult resultCallback = EventProcessResult.UNKNOWN;

        LSAction<UserRegistrationEvent> callback = (evt) => {
            executionOrder.Add("Handler1_Resumed");
            resultCallback = evt.Resume();
        };
        //THIS COULD BE DONE IN EVENT-SCOPED
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg
                .Handler(ctx => {
                    executionOrder.Add("Handler1_Start");
                    if (ctx.Event is UserRegistrationEvent ure)
                        // Resume immediately to simulate pseudo-sequential case
                        callback(ure);
                    return HandlerProcessResult.WAITING;
                }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg
                .Handler(ctx => {
                    executionOrder.Add("Handler2_Executed");
                    return HandlerProcessResult.SUCCESS;
                }))
        );

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        var result = eventInstance.Dispatch();

        Assert.That(resultCallback, Is.EqualTo(EventProcessResult.WAITING)); // callback should returns WAITING since was called before the dispatch completes
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS)); // dispatch will complete successfully since resume was called before
        Assert.That(executionOrder.Count, Is.EqualTo(3));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Start"));
        Assert.That(executionOrder[1], Is.EqualTo("Handler1_Resumed"));
        // Handler1 should start and resume before Handler2 executes
        Assert.That(executionOrder[2], Is.EqualTo("Handler2_Executed"));


        // Assert.That(executionOrder, Contains.Item("Handler1_Start")); //this is confuse
        // Assert.That(executionOrder.Any(item => item.StartsWith("Handler1_Resumed_Result_")), Is.True); //this is confuse
        // Handler2 should execute after Resume() is called
        // Assert.That(executionOrder, Contains.Item("Handler2_Executed")); //this is confuse
        Console.WriteLine($"ConfigurePhase pseudo-sequential: {string.Join(" → ", executionOrder)}");
    }

    //Test ConfigurePhase sequential waiting - resume after dispatch
    [Test]
    public void TestConfigurePhaseSequentialWaiting() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg
                .Handler(ctx => {
                    executionOrder.Add("Handler1_Waiting");
                    return HandlerProcessResult.WAITING;
                }))
            .OnPhase<LSEventBusinessState.ConfigurePhaseState>(cReg => cReg
                .Handler(ctx => {
                    executionOrder.Add("Handler2_Executed");
                    return HandlerProcessResult.SUCCESS;
                }))
        );

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Start dispatch
        var result = eventInstance.Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.WAITING));

        Assert.That(executionOrder.Count, Is.EqualTo(1));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting"));

        // Wait and verify Handler2 hasn't executed (blocked by sequential waiting)
        //Assert.That(executionOrder, Is.EqualTo(new[] { "Handler1_Waiting" })); //this is confuse

        // Resume and verify Handler2 now executes
        result = eventInstance.Resume();
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));

        Assert.That(executionOrder.Count, Is.EqualTo(2));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting"));
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Executed"));

        Console.WriteLine($"ConfigurePhase operation: {string.Join(" → ", executionOrder)}");
    }

    //Test ExecutePhase parallel waiting - resume before handler completes
    [Test]
    public void TestExecutePhaseParallelWaiting_PseudoSequential() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        EventProcessResult resumeResult = EventProcessResult.UNKNOWN;

        System.Func<UserRegistrationEvent, EventProcessResult> callback = (evt) => {
            resumeResult = evt.Resume();
            executionOrder.Add($"Handler1_Resumed_Result_{resumeResult}");
            return resumeResult;
        };
        //this can be done with multiple OnPhase chain calls or multiple "eReg" params
        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler1_Waiting");
                    // Resume immediately to simulate pseudo-sequential case
                    if (ctx.Event is UserRegistrationEvent ure)
                        callback(ure);
                    return HandlerProcessResult.WAITING;
                }),
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler2_Executed");
                    return HandlerProcessResult.SUCCESS;
                }))
            );

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        var result = eventInstance.Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS)); // dispatch returns SUCCESS

        Assert.That(executionOrder, Contains.Item("Handler1_Waiting")); // confuse but should work
        Assert.That(executionOrder, Contains.Item("Handler2_Executed")); //confuse but should work
        Assert.That(executionOrder.Any(item => item.StartsWith("Handler1_Resumed_Result_")), Is.True); // confuse but should work

        Assert.That(executionOrder.Count, Is.EqualTo(3));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting")); // Handler1 starts first
        Assert.That(executionOrder[1], Is.EqualTo($"Handler1_Resumed_Result_{EventProcessResult.SUCCESS}")); // Handler1 resumes second
        Assert.That(executionOrder[2], Is.EqualTo("Handler2_Executed")); // Handler2 executes last
        // In ExecutePhase, Handler2 executes immediately despite Handler1 waiting
        Console.WriteLine($"ExecutePhase : {string.Join(" → ", executionOrder)}");
    }

    //Test ExecutePhase parallel waiting - resume after dispatch
    [Test]
    public void TestExecutePhaseParallelWaiting() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        LSAction? resumeCallback = null;
        EventProcessResult resumeResult = EventProcessResult.UNKNOWN;

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg =>
                eReg.Handler(ctx => {
                    executionOrder.Add("Handler1_Waiting");
                    resumeCallback = () => {
                        resumeResult = ctx.Resume();
                        executionOrder.Add($"Handler1_Resumed_Result_{resumeResult}");

                    };
                    return HandlerProcessResult.WAITING;
                }),
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler2_Executed");
                    return HandlerProcessResult.SUCCESS;
                })));

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Start dispatch in background
        var result = eventInstance.Dispatch();

        // Wait - Handler2 should execute immediately despite Handler1 waiting (parallel behavior)
        Assert.That(executionOrder, Is.EqualTo(new[] { "Handler1_Waiting", "Handler2_Executed" })); // this is confuse
        Assert.That(result, Is.EqualTo(EventProcessResult.WAITING), "Dispatch should still be waiting for Handler1");

        Assert.That(executionOrder.Count, Is.EqualTo(2));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting"));
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Executed"));

        // Resume and verify event completes
        resumeCallback?.Invoke();

        Assert.That(resumeResult, Is.EqualTo(EventProcessResult.SUCCESS), "Resume() should have resumed Handler1");

        Assert.That(executionOrder.Count, Is.EqualTo(3));
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting"));
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Executed"));
        Assert.That(executionOrder[2], Is.EqualTo($"Handler1_Resumed_Result_{EventProcessResult.SUCCESS}")); // Handler1 resumes last

        Console.WriteLine($"ExecutePhase resume: {string.Join(" → ", executionOrder)}");
    }

    //Test ExecutePhase multiple parallel waiting handlers
    [Test]
    public void TestExecutePhaseMultipleParallelWaiting() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg.Handler(ctx => {
                executionOrder.Add("Handler1_Waiting");
                return HandlerProcessResult.WAITING;
            }),
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler2_Success");
                    return HandlerProcessResult.SUCCESS;
                }),
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler3_Waiting");
                    return HandlerProcessResult.WAITING;
                }))
            .OnState<LSEventSucceedState>(completeReg => completeReg.Handler(evt => {
                executionOrder.Add("SucceedState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                executionOrder.Add("CompletedState");
            }))
            );

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Start dispatch
        var result = eventInstance.Dispatch();
        // all business handlers should execute, but event cannot conclude until all waiting handlers are resumed
        Assert.That(result, Is.EqualTo(EventProcessResult.WAITING), "Should be waiting for handlers");
        Assert.That(executionOrder.Count, Is.EqualTo(3)); //only the 3 business handlers should have executed
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting"));
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Success"));
        Assert.That(executionOrder[2], Is.EqualTo("Handler3_Waiting"));

        //Assert.That(executionOrder, Is.EqualTo(new[] { "Handler1_Waiting", "Handler2_Success", "Handler3_Waiting" }));

        // Resume first waiting handler
        var resumeResult1 = eventInstance.Resume();
        Assert.That(resumeResult1, Is.EqualTo(EventProcessResult.WAITING)); // Still waiting for second handler
        Assert.That(executionOrder.Count, Is.EqualTo(3)); // no new handlers should have executed yet

        // Check if still waiting or completed
        var resumeResult2 = eventInstance.Resume();
        Assert.That(resumeResult2, Is.EqualTo(EventProcessResult.SUCCESS)); // Should succeed now
        Assert.That(executionOrder.Count, Is.EqualTo(5)); // SucceedState and CompletedState should have executed
        Assert.That(executionOrder[3], Is.EqualTo("SucceedState"));
        Assert.That(executionOrder[4], Is.EqualTo("CompletedState"));

        Console.WriteLine($"ExecutePhase multiple parallel: {string.Join(" → ", executionOrder)}");
        Console.WriteLine($"Resume results: {resumeResult1}");
    }

    //Test CleanupPhase waiting doesn't affect business result
    [Test]
    public void TestCleanupPhaseWaitingDoesNotAffectBusinessResult() {
        List<string> executionOrder = new List<string>();

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ValidatePhaseState>(vReg => vReg
                .Handler(ctx => {
                    executionOrder.Add("Validate_Success");
                    return HandlerProcessResult.SUCCESS;
                }))
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(eReg => eReg
                .Handler(ctx => {
                    executionOrder.Add("Execute_Success");
                    return HandlerProcessResult.SUCCESS;
                }))
            .OnPhase<LSEventBusinessState.CleanupPhaseState>(
                cReg => cReg.Handler(ctx => {
                    executionOrder.Add("Cleanup_Waiting");
                    return HandlerProcessResult.WAITING;
                }),
                cReg => cReg.Handler(ctx => {
                    executionOrder.Add("Cleanup_Success");
                    return HandlerProcessResult.SUCCESS;
                }))
            );

        var user = new User("test@example.com", "password123");
        var userEvent = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        var result = userEvent.Dispatch();

        // Business should succeed despite cleanup waiting (cleanup failure doesn't affect business result)
        Assert.That(result, Is.EqualTo(EventProcessResult.SUCCESS));
        Assert.That(executionOrder, Is.EqualTo(new[] { "Validate_Success", "Execute_Success", "Cleanup_Waiting", "Cleanup_Success" }));
        Console.WriteLine($"CleanupPhase waiting: {string.Join(" → ", executionOrder)}");
    }

    //Test WAITING behavior with Cancel() call
    [Test]
    public void TestWaitingBehaviorWithCancel() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;

        LSAction? cancelCallback = null;

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler1_Waiting");
                    cancelCallback = () => {
                        executionOrder.Add("Handler1_Cancelled");
                        eventInstance?.Cancel();
                    };
                    return HandlerProcessResult.WAITING;
                }),
                 eReg => eReg.Handler(ctx => {
                     executionOrder.Add("Handler2_Executed");
                     return HandlerProcessResult.SUCCESS;
                 }))
            .OnState<LSEventCancelledState>(cancelReg => cancelReg.Handler(evt => {
                executionOrder.Add("CancelledState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                executionOrder.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Start dispatch
        var result = eventInstance.Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.WAITING)); // Dispatch returns WAITING initially
        Assert.That(executionOrder.Count, Is.EqualTo(2));

        Assert.That(executionOrder, Contains.Item("Handler1_Waiting")); // check in case is out of order
        Assert.That(executionOrder, Contains.Item("Handler2_Executed")); // check in case is out of order

        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting")); // Handler1 should start first
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Executed")); // Handler2 should execute immediately despite Handler1 waiting (parallel behavior)

        // trigger the cancelCallback, it should have been set by the first handler
        cancelCallback?.Invoke();

        Assert.That(executionOrder, Contains.Item("Handler1_Cancelled")); // check in case is out of order
        Assert.That(executionOrder, Contains.Item("CancelledState")); // check in case is out of order
        Assert.That(executionOrder, Contains.Item("CompletedState")); // check in case is out of order

        Assert.That(executionOrder.Count, Is.EqualTo(5)); // total of 5 steps should have executed
        Assert.That(executionOrder[2], Is.EqualTo("Handler1_Cancelled"));
        Assert.That(executionOrder[3], Is.EqualTo("CancelledState"));
        Assert.That(executionOrder[4], Is.EqualTo("CompletedState"));

        Console.WriteLine($"WAITING with Cancel: {string.Join(" → ", executionOrder)}");
    }

    //Test WAITING behavior with Fail() call
    [Test]
    public void TestWaitingBehaviorWithFail() {
        List<string> executionOrder = new List<string>();
        UserRegistrationEvent? eventInstance = null;
        LSAction? failCallback = null;
        EventProcessResult failResult = EventProcessResult.UNKNOWN;

        _dispatcher!.ForEvent<UserRegistrationEvent>(registration => registration
            .OnPhase<LSEventBusinessState.ExecutePhaseState>(
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler1_Waiting");
                    // Simulate external failure after delay
                    failCallback = () => {
                        executionOrder.Add("Handler1_Failed");
                        failResult = ctx.Fail();
                    };
                    return HandlerProcessResult.WAITING;
                }),
                eReg => eReg.Handler(ctx => {
                    executionOrder.Add("Handler2_Executed");
                    return HandlerProcessResult.SUCCESS;
                }))
            .OnState<LSEventSucceedState>(successReg => successReg.Handler(evt => {
                executionOrder.Add("SucceedState");
            }))
            .OnState<LSEventCompletedState>(completeReg => completeReg.Handler(evt => {
                executionOrder.Add("CompletedState");
            })));

        var user = new User("test@example.com", "password123");
        eventInstance = new UserRegistrationEvent(user, new LSEventOptions(_dispatcher));

        // Start dispatch in background
        var result = eventInstance.Dispatch();
        Assert.That(result, Is.EqualTo(EventProcessResult.WAITING)); // Dispatch returns WAITING initially
        Assert.That(executionOrder.Count, Is.EqualTo(2));
        Assert.That(executionOrder, Contains.Item("Handler1_Waiting"));
        Assert.That(executionOrder, Contains.Item("Handler2_Executed")); // ExecutePhase should run in parallel
        Assert.That(executionOrder[0], Is.EqualTo("Handler1_Waiting")); // Handler1 should start first
        Assert.That(executionOrder[1], Is.EqualTo("Handler2_Executed")); // Handler2 should execute immediately despite Handler1 waiting (parallel behavior)

        // Trigger the failCallback to simulate external failure
        failCallback?.Invoke();
        Assert.That(failResult, Is.EqualTo(EventProcessResult.FAILURE), "Fail() should return FAILURE");
        Assert.That(executionOrder.Count, Is.EqualTo(4));
        Assert.That(executionOrder[2], Is.EqualTo("Handler1_Failed"));
        Assert.That(executionOrder[3], Is.EqualTo("CompletedState"));

        Assert.That(executionOrder, Contains.Item("Handler1_Failed"));
        Assert.That(executionOrder, Does.Not.Contain("SucceedState")); // Should skip due to failure
        Assert.That(executionOrder, Contains.Item("CompletedState"));
        Console.WriteLine($"WAITING with Fail: {string.Join(" → ", executionOrder)}");
    }
    #endregion
}
