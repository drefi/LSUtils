# LSEventSystem Logging Integration Examples

This document shows how the new logging system integrates with existing LSEventSystem functionality using real examples from the test suite.

## Integration with Existing Event Processing

### Basic Event Processing with Automatic Logging

```csharp
[Test]
public void Test_Event_Processing_With_Automatic_Logging() {
    // Set logging level to see all internal operations
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    LSEventLogger.Instance.ClearProviders();
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
    
    var dispatcher = new LSDispatcher();
    var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
    var userEvent = new TestUserRegistrationEvent(user, "web");

    // This will generate comprehensive logging automatically
    var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
        .OnValidatePhase((evt, ctx) => {
            // The dispatcher automatically logs:
            // [DEBUG] ExecuteHandler: Executing handler {id} in phase VALIDATE
            // [DEBUG] ExecuteHandler: Handler {id} returned CONTINUE
            
            // Add your custom business logging
            LSEventLogger.Instance.Info("Email validation passed for user registration",
                properties: new Dictionary<string, object> {
                    ["user_email"] = evt.Instance.Email,
                    ["registration_source"] = evt.RegistrationSource
                });
                
            return LSHandlerResult.CONTINUE;
        })
        .OnExecutePhase((evt, ctx) => {
            // Custom execution logging
            LSEventLogger.Instance.Info("Creating user account",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["user_name"] = evt.Instance.Name,
                    ["user_age"] = evt.Instance.Age
                });
                
            evt.SetData("user.created", true);
            return LSHandlerResult.CONTINUE;
        })
        .Dispatch();

    Assert.That(success, Is.True);
    // Console will show:
    // - DEBUG messages for all internal dispatcher operations
    // - INFO messages for your custom business logic
    // - Event lifecycle completion logging
}
```

### Exception Handling with CRITICAL Logging

Based on the existing `Test_Handler_Exception_Handling` test:

```csharp
[Test]
public void Test_Exception_Handling_With_Logging() {
    // Configure logging to show all levels
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    
    var dispatcher = new LSDispatcher();
    var startupEvent = new TestSystemStartupEvent("exception-test");
    bool exceptionThrown = false;

    try {
        startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
            .OnExecutePhase((evt, ctx) => {
                // Add custom logging before the exception
                LSEventLogger.Instance.Info("About to perform risky operation");
                
                // This exception will be automatically logged as CRITICAL
                throw new InvalidOperationException("Test exception");
            })
            .Dispatch();
    } catch (InvalidOperationException ex) {
        exceptionThrown = true;
        
        // You can add additional logging after catching
        LSEventLogger.Instance.Error("Exception caught in test", ex,
            properties: new Dictionary<string, object> {
                ["test_name"] = nameof(Test_Exception_Handling_With_Logging),
                ["exception_type"] = ex.GetType().Name
            });
    }

    Assert.That(exceptionThrown, Is.True);
    
    // Console output will show:
    // [INFO] About to perform risky operation
    // [CRITICAL] Handler {id} threw exception in phase EXECUTE
    //   Exception: InvalidOperationException: Test exception
    //   Stack Trace: [full stack trace]
    // [ERROR] Exception caught in test
}
```

### Async Operations with Detailed Logging

Based on the existing `Test_Asynchronous_Operation_Immediate_Resume` test:

```csharp
[Test]
public void Test_Async_Operations_With_Logging() {
    // Enable detailed logging for async operations
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    
    var dispatcher = new LSDispatcher();
    var startupEvent = new TestSystemStartupEvent("async-logging-test");
    bool resumeCalled = false;
    bool asyncOperationStarted = false;

    var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
        .OnExecutePhase((evt, ctx) => {
            asyncOperationStarted = true;
            
            // Log the start of async operation
            LSEventLogger.Instance.Info("Starting async operation",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["operation_type"] = "immediate_completion",
                    ["event_version"] = evt.Version
                });
            
            // Simulate immediate async completion
            SimulateImmediateAsyncOperation(evt, true, (result) => {
                if (result) {
                    LSEventLogger.Instance.Info("Async operation completed successfully",
                        eventId: evt.ID);
                    evt.Resume();
                } else {
                    LSEventLogger.Instance.Warning("Async operation failed",
                        eventId: evt.ID);
                    evt.Abort();
                }
            });
            
            LSEventLogger.Instance.Debug("Handler returning WAITING state");
            return LSHandlerResult.WAITING;
        })
        .OnSuccess(evt => {
            resumeCalled = true;
            LSEventLogger.Instance.Info("Event completed successfully after async operation",
                eventId: evt.ID);
        })
        .Dispatch();

    Assert.That(success, Is.True);
    Assert.That(asyncOperationStarted, Is.True);
    Assert.That(resumeCalled, Is.True);
    
    // Console will show the complete async operation lifecycle
}
```

### Event Cancellation with WARNING Logging

Based on the existing `Test_Event_Cancellation` test:

```csharp
[Test]
public void Test_Event_Cancellation_With_Logging() {
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    
    var dispatcher = new LSDispatcher();
    var user = new TestUser { Email = "", Name = "Test User", Age = 25 }; // Invalid email

    bool validationCalled = false;
    bool executionCalled = false;

    var userEvent = new TestUserRegistrationEvent(user, "web");
    var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
        .OnValidatePhase((evt, ctx) => {
            validationCalled = true;
            
            if (string.IsNullOrEmpty(evt.Instance.Email)) {
                // Log the validation failure
                LSEventLogger.Instance.Warning("Email validation failed - cancelling event",
                    eventId: evt.ID,
                    properties: new Dictionary<string, object> {
                        ["validation_rule"] = "email_required",
                        ["user_name"] = evt.Instance.Name,
                        ["registration_source"] = evt.RegistrationSource
                    });
                
                evt.SetErrorMessage("Email is required");
                return LSHandlerResult.CANCEL; // Dispatcher logs this as WARNING
            }
            return LSHandlerResult.CONTINUE;
        })
        .OnExecutePhase((evt, ctx) => {
            executionCalled = true;
            
            // This won't be called due to cancellation
            LSEventLogger.Instance.Info("Executing user registration");
            return LSHandlerResult.CONTINUE;
        })
        .OnCancel(evt => {
            LSEventLogger.Instance.Info("Event cancellation handler executed",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["cancellation_reason"] = evt.ErrorMessage
                });
        })
        .Dispatch();

    Assert.That(success, Is.False);
    Assert.That(validationCalled, Is.True);
    Assert.That(executionCalled, Is.False);
    Assert.That(userEvent.IsCancelled, Is.True);
    
    // Console output will show:
    // [WARNING] Email validation failed - cancelling event
    // [WARNING] ProcessPhaseResult: Phase VALIDATE requested cancellation
    // [INFO] Event cancellation handler executed
}
```

### Priority-Based Handler Execution with Logging

Based on the existing `Test_Priority_Based_Execution` test:

```csharp
[Test]
public void Test_Priority_Execution_With_Logging() {
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    
    var dispatcher = new LSDispatcher();
    var user = new TestUser { Email = "test@example.com", Name = "Test User", Age = 25 };
    var executionOrder = new List<string>();

    var userEvent = new TestUserRegistrationEvent(user, "web");
    var success = userEvent.WithCallbacks<TestUserRegistrationEvent>(dispatcher)
        .OnValidatePhase((evt, ctx) => {
            executionOrder.Add("Normal");
            LSEventLogger.Instance.Debug("Normal priority validation handler executed",
                properties: new Dictionary<string, object> {
                    ["priority"] = "NORMAL",
                    ["execution_order"] = executionOrder.Count
                });
            return LSHandlerResult.CONTINUE;
        }, LSPhasePriority.NORMAL)
        .OnValidatePhase((evt, ctx) => {
            executionOrder.Add("Critical");
            LSEventLogger.Instance.Debug("Critical priority validation handler executed",
                properties: new Dictionary<string, object> {
                    ["priority"] = "CRITICAL",
                    ["execution_order"] = executionOrder.Count
                });
            return LSHandlerResult.CONTINUE;
        }, LSPhasePriority.CRITICAL)
        .OnValidatePhase((evt, ctx) => {
            executionOrder.Add("High");
            LSEventLogger.Instance.Debug("High priority validation handler executed",
                properties: new Dictionary<string, object> {
                    ["priority"] = "HIGH",
                    ["execution_order"] = executionOrder.Count
                });
            return LSHandlerResult.CONTINUE;
        }, LSPhasePriority.HIGH)
        .Dispatch();

    Assert.That(success, Is.True);
    Assert.That(executionOrder[0], Is.EqualTo("Critical"));
    Assert.That(executionOrder[1], Is.EqualTo("High"));
    Assert.That(executionOrder[2], Is.EqualTo("Normal"));
    
    // Debug logs will show the exact execution order with priorities
}
```

### Data Persistence Across Phases with Logging

Based on the existing `Test_Event_Data_Persistence_Across_Phases` test:

```csharp
[Test]
public void Test_Data_Persistence_With_Logging() {
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    
    var dispatcher = new LSDispatcher();
    var startupEvent = new TestSystemStartupEvent("data-persistence");

    var success = startupEvent.WithCallbacks<TestSystemStartupEvent>(dispatcher)
        .OnValidatePhase((evt, ctx) => {
            evt.SetData("phase_data", "validate");
            evt.SetData("timestamp", DateTime.UtcNow);
            
            LSEventLogger.Instance.Debug("Data set in VALIDATE phase",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["phase"] = "VALIDATE",
                    ["data_keys"] = new[] { "phase_data", "timestamp" }
                });
                
            return LSHandlerResult.CONTINUE;
        })
        .OnExecutePhase((evt, ctx) => {
            var data = evt.GetData<string>("phase_data");
            var timestamp = evt.GetData<DateTime>("timestamp");
            
            Assert.That(data, Is.EqualTo("validate"));
            
            LSEventLogger.Instance.Debug("Data retrieved in EXECUTE phase",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["phase"] = "EXECUTE",
                    ["retrieved_data"] = data,
                    ["data_age_ms"] = (DateTime.UtcNow - timestamp).TotalMilliseconds
                });
            
            evt.SetData("phase_data", "execute");
            return LSHandlerResult.CONTINUE;
        })
        .OnSuccess(evt => {
            var finalData = evt.GetData<string>("phase_data");
            
            LSEventLogger.Instance.Info("Event completed with data persistence verified",
                eventId: evt.ID,
                properties: new Dictionary<string, object> {
                    ["final_data"] = finalData,
                    ["phases_completed"] = new[] { "VALIDATE", "EXECUTE", "SUCCESS" }
                });
                
            Assert.That(finalData, Is.EqualTo("execute"));
        })
        .Dispatch();

    Assert.That(success, Is.True);
}
```

## Configuration Examples for Different Scenarios

### Development Environment

```csharp
[SetUp]
public void SetupDevelopmentLogging() {
    LSEventLogger.Instance.ClearProviders();
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    LSEventLogger.Instance.IsEnabled = true;
}
```

### Testing Environment (Less Verbose)

```csharp
[SetUp] 
public void SetupTestingLogging() {
    LSEventLogger.Instance.ClearProviders();
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.WARNING; // Only warnings and above
    LSEventLogger.Instance.IsEnabled = true;
}
```

### Godot Game Development

```csharp
[SetUp]
public void SetupGodotLogging() {
    LSEventLogger.Instance.ClearProviders();
    
    // Auto-detect Godot environment
    if (new LSGodotLogProvider().IsAvailable) {
        LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());
        LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;
    } else {
        // Fallback to console for editor testing
        LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
        LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    }
}
```

### Performance Testing (Silent Mode)

```csharp
[SetUp]
public void SetupPerformanceLogging() {
    // Disable all logging for performance tests
    LSEventLogger.Instance.DisableLogging();
}
```

## Real-World Integration Examples

### User Registration System

```csharp
public class UserRegistrationService {
    private readonly LSDispatcher _dispatcher;
    
    public UserRegistrationService(LSDispatcher dispatcher) {
        _dispatcher = dispatcher;
        
        // Configure logging for production use
        LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;
    }
    
    public async Task<bool> RegisterUserAsync(User user, string source) {
        var registrationEvent = new UserRegistrationEvent(user, source);
        
        var success = registrationEvent.WithCallbacks<UserRegistrationEvent>(_dispatcher)
            .OnValidatePhase((evt, ctx) => {
                // Business validation with logging
                if (string.IsNullOrEmpty(evt.Instance.Email)) {
                    LSEventLogger.Instance.Warning("User registration failed - missing email",
                        eventId: evt.ID,
                        properties: new Dictionary<string, object> {
                            ["user_name"] = evt.Instance.Name,
                            ["source"] = evt.RegistrationSource,
                            ["validation_rule"] = "email_required"
                        });
                    return LSHandlerResult.CANCEL;
                }
                
                LSEventLogger.Instance.Info("User validation passed",
                    eventId: evt.ID,
                    properties: new Dictionary<string, object> {
                        ["user_email"] = evt.Instance.Email,
                        ["source"] = evt.RegistrationSource
                    });
                    
                return LSHandlerResult.CONTINUE;
            })
            .OnExecutePhase((evt, ctx) => {
                try {
                    // Database operation
                    CreateUserInDatabase(evt.Instance);
                    
                    LSEventLogger.Instance.Info("User created in database",
                        eventId: evt.ID,
                        properties: new Dictionary<string, object> {
                            ["user_id"] = evt.Instance.Id,
                            ["database_operation"] = "insert_user"
                        });
                        
                    return LSHandlerResult.CONTINUE;
                } catch (Exception ex) {
                    LSEventLogger.Instance.Error("Database operation failed", ex,
                        eventId: evt.ID,
                        properties: new Dictionary<string, object> {
                            ["operation"] = "create_user",
                            ["user_email"] = evt.Instance.Email
                        });
                    throw;
                }
            })
            .OnSuccess(evt => {
                LSEventLogger.Instance.Info("User registration completed successfully",
                    eventId: evt.ID,
                    properties: new Dictionary<string, object> {
                        ["user_id"] = evt.Instance.Id,
                        ["total_duration_ms"] = (DateTime.UtcNow - evt.CreatedAt).TotalMilliseconds
                    });
            })
            .OnCancel(evt => {
                LSEventLogger.Instance.Warning("User registration cancelled",
                    eventId: evt.ID,
                    properties: new Dictionary<string, object> {
                        ["cancellation_reason"] = evt.ErrorMessage,
                        ["source"] = evt.RegistrationSource
                    });
            })
            .Dispatch();
            
        return success;
    }
}
```

This integration shows how the new logging system seamlessly works with existing LSEventSystem patterns while providing comprehensive observability into event processing, business logic execution, and system behavior.
