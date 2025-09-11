# LSEventSystem v4 - Comprehensive Documentation

## Overview

LSEventSystem v4 is a clean, state-machine-based event processing framework designed for robust, scalable event handling in .NET applications. It provides a simplified yet powerful approach to event processing through sequential phases with proper failure handling, cancellation support, and asynchronous operation management.

## Table of Contents

1. [Key Features](#key-features)
2. [Architecture Overview](#architecture-overview)
3. [Core Components](#core-components)
4. [Getting Started](#getting-started)
5. [Event Lifecycle](#event-lifecycle)
6. [Handler Registration](#handler-registration)
7. [Phase-Based Processing](#phase-based-processing)
8. [State Management](#state-management)
9. [Error Handling](#error-handling)
10. [Asynchronous Operations](#asynchronous-operations)
11. [Best Practices](#best-practices)
12. [Examples](#examples)
13. [API Reference](#api-reference)
14. [Migration Guide](#migration-guide)
15. [Performance Considerations](#performance-considerations)
16. [Troubleshooting](#troubleshooting)

## Key Features

### ✅ **Clean State Machine Architecture**

- Sequential phase execution: VALIDATE → CONFIGURE → EXECUTE → CLEANUP
- Clear state transitions with well-defined outcomes
- Terminal states for completion, cancellation, and failure scenarios

### ✅ **Flexible Handler Registration**

- Global handlers via dispatcher registration
- Event-scoped handlers for dynamic behavior
- Priority-based execution within phases
- Type-safe registration with compile-time validation

### ✅ **Robust Error Handling**

- Distinction between recoverable failures and critical cancellations
- Graceful degradation with continued processing on failures
- Comprehensive exception handling and logging

### ✅ **Asynchronous Operation Support**

- WAITING state for external input requirements
- Resume/Cancel/Fail operations for async coordination
- No internal threading - external control required

### ✅ **Thread-Safe Data Management**

- Concurrent dictionary for event data storage
- Type-safe data access with TryGetData patterns
- Immutable event state from handler perspective

### ✅ **Comprehensive Testing Support**

- Built-in test infrastructure
- Handler execution tracking and validation
- State transition verification

## Architecture Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│                        LSEventSystem v4                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐    ┌──────────────────┐    ┌───────────────┐  │
│  │   Events    │    │   Dispatcher     │    │   Handlers    │  │
│  │             │    │                  │    │               │  │
│  │ BaseEvent   │◄──►│ LSESDispatcher   │◄──►│ PhaseHandler  │  │
│  │ ILSEvent    │    │ Registration     │    │ StateHandler  │  │
│  │             │    │ Management       │    │               │  │
│  └─────────────┘    └──────────────────┘    └───────────────┘  │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                State Machine Core                          │  │
│  │                                                             │  │
│  │  ┌─────────────────────────────────────────────────────┐   │  │
│  │  │               BusinessState                         │   │  │
│  │  │                                                     │   │  │
│  │  │  Validate → Configure → Execute → Cleanup          │   │  │
│  │  │     ↓           ↓          ↓         ↓             │   │  │
│  │  │   [Fail]    [Wait/Fail] [Wait/Fail] [Always]      │   │  │
│  │  └─────────────────────────────────────────────────────┘   │  │
│  │           ↓                    ↓                    ↓       │  │
│  │  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐     │  │
│  │  │ SucceedState│    │CancelledState│   │CompletedState│    │  │
│  │  └─────────────┘    └─────────────┘    └─────────────┘     │  │
│  └─────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### BaseEvent

Abstract base class for all events in the system. Provides:

- Unique identification and timestamp tracking
- Thread-safe data storage and retrieval
- Integration with dispatcher and state machine
- Event-scoped handler configuration

### LSESDispatcher

Central handler registration and management system. Features:

- Singleton pattern for application-wide access
- Type-safe handler registration
- Support for both phase and state handlers
- Internal handler storage and retrieval

### EventSystemContext

Coordination point for event processing. Manages:

- State machine transitions
- Handler execution context
- Event lifecycle tracking
- External interaction points (Resume/Cancel/Fail)

### Phase States

Individual phases in the business processing pipeline:

- **ValidatePhaseState**: Input validation and early checks
- **ConfigurePhaseState**: Resource allocation and setup
- **ExecutePhaseState**: Core business logic execution
- **CleanupPhaseState**: Finalization and resource cleanup

## Getting Started

### 1. Define Your Event

```csharp
public class UserRegistrationEvent : BaseEvent {
    public string Email { get; }
    public string Name { get; }
    public string RegistrationSource { get; }

    public UserRegistrationEvent(LSESDispatcher dispatcher, string email, string name, string source)
        : base(dispatcher) {
        Email = email;
        Name = name;
        RegistrationSource = source;
        
        // Store initial data
        SetData("email", email);
        SetData("name", name);
        SetData("source", source);
    }
}
```

### 2. Register Global Handlers

```csharp
var dispatcher = LSESDispatcher.Singleton;

// Register validation handler
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSESPriority.HIGH)
        .Handler(ctx => {
            var email = ctx.Event.GetData<string>("email");
            if (string.IsNullOrEmpty(email) || !email.Contains("@")) {
                ctx.Event.SetData("error", "Invalid email format");
                return HandlerProcessResult.FAILURE;
            }
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Register execution handler
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var email = ctx.Event.GetData<string>("email");
            var name = ctx.Event.GetData<string>("name");
            
            // Create user account
            var userId = CreateUserAccount(email, name);
            ctx.Event.SetData("user_id", userId);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

### 3. Create and Dispatch Events

```csharp
// Create event with optional event-scoped handlers
var registrationEvent = new UserRegistrationEvent(
    dispatcher, 
    "user@example.com", 
    "John Doe", 
    "web_form")
    .WithPhaseCallbacks<BusinessState.CleanupPhaseState>(
        register => register
            .Handler(ctx => {
                // Event-specific cleanup logic
                LogRegistrationAttempt(ctx.Event);
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    );

// Dispatch and handle result
var result = registrationEvent.Dispatch();

switch (result) {
    case EventProcessResult.SUCCESS:
        Console.WriteLine("User registered successfully");
        var userId = registrationEvent.GetData<int>("user_id");
        break;
        
    case EventProcessResult.FAILURE:
        Console.WriteLine("Registration failed but was handled gracefully");
        var error = registrationEvent.GetData<string>("error");
        break;
        
    case EventProcessResult.CANCELLED:
        Console.WriteLine("Registration was cancelled");
        break;
        
    case EventProcessResult.WAITING:
        Console.WriteLine("Registration is waiting for external input");
        // Handle async scenario
        break;
}
```

## Event Lifecycle

### 1. **Creation Phase**

- Event instantiation with required data
- Optional event-scoped handler configuration
- Data initialization and validation

### 2. **Registration Phase**

- Handler retrieval from dispatcher
- Event-scoped handler integration
- Context creation and initialization

### 3. **Processing Phase**

- State machine initialization (BusinessState)
- Sequential phase execution
- Handler execution with priority ordering
- State transitions based on results

### 4. **Completion Phase**

- Transition to terminal state (Succeed/Cancelled/Completed)
- Final handler execution
- Resource cleanup and finalization

## Handler Registration

### Global Handler Registration

Global handlers are registered with the dispatcher and execute for all events of the specified type:

```csharp
// Single handler registration
var handlerId = dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSESPriority.CRITICAL)
        .When((evt, entry) => evt.GetData<string>("source") == "web_form") // Conditional execution
        .Handler(ctx => {
            // Validation logic specific to web form registrations
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Multiple handler registration
var handlerIds = dispatcher.ForEvent<UserRegistrationEvent>(register => register
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.HIGH)
        .Handler(ctx => ValidateEmailFormat(ctx))
        .Build())
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.NORMAL)
        .Handler(ctx => CheckEmailAvailability(ctx))
        .Build())
    .OnPhase<BusinessState.ExecutePhaseState>(phase => phase
        .Handler(ctx => CreateUserAccount(ctx))
        .Build())
    .OnState<SucceedState>(state => state
        .Handler(evt => SendWelcomeEmail(evt))
        .Build())
    .Register());
```

### Event-Scoped Handler Registration

Event-scoped handlers are specific to individual event instances and execute alongside global handlers:

```csharp
var event = new UserRegistrationEvent(dispatcher, email, name, "api")
    .WithPhaseCallbacks<BusinessState.ValidatePhaseState>(
        // Custom validation for API registrations
        register => register
            .WithPriority(LSESPriority.HIGH)
            .Handler(ctx => {
                var apiKey = ctx.Event.GetData<string>("api_key");
                if (!ValidateApiKey(apiKey)) {
                    return HandlerProcessResult.CANCELLED;
                }
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    )
    .WithPhaseCallbacks<BusinessState.ExecutePhaseState>(
        // Custom execution logic
        register => register
            .Handler(ctx => {
                // API-specific user creation logic
                return CreateApiUser(ctx);
            })
            .Build()
    );
```

### Priority-Based Execution

Handlers within each phase execute in priority order:

1. **CRITICAL** (0) - System-critical operations, security checks
2. **HIGH** (1) - Important business logic
3. **NORMAL** (2) - Standard operations (default)
4. **LOW** (3) - Nice-to-have features
5. **BACKGROUND** (4) - Logging, metrics, cleanup

```csharp
dispatcher.ForEvent<UserRegistrationEvent>(register => register
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.CRITICAL)
        .Handler(ctx => SecurityValidation(ctx)) // Executes first
        .Build())
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.HIGH)
        .Handler(ctx => BusinessValidation(ctx)) // Executes second
        .Build())
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.BACKGROUND)
        .Handler(ctx => LogValidation(ctx)) // Executes last
        .Build())
    .Register());
```

## Phase-Based Processing

### Validate Phase

**Purpose**: Input validation, permission checks, early validation logic
**Behavior**: Any failure stops processing immediately
**Typical Usage**: Security checks, data format validation, permission verification

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSESPriority.CRITICAL)
        .Handler(ctx => {
            // Security validation - must pass
            if (!IsUserAllowedToRegister(ctx.Event)) {
                ctx.Event.SetData("security_error", "Registration not permitted");
                return HandlerProcessResult.CANCELLED; // Stops processing immediately
            }
            
            // Data validation
            var email = ctx.Event.GetData<string>("email");
            if (!IsValidEmailFormat(email)) {
                ctx.Event.SetData("validation_errors", new[] { "Invalid email format" });
                return HandlerProcessResult.FAILURE; // Fails validation, stops processing
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

### Configure Phase

**Purpose**: Configuration, setup, resource allocation, state preparation
**Behavior**: Failures don't stop processing but trigger cleanup
**Waiting Support**: Can pause for external configuration

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ConfigurePhaseState>(
    register => register
        .Handler(ctx => {
            // Allocate resources
            var databaseConnection = AcquireDatabaseConnection();
            ctx.Event.SetData("db_connection", databaseConnection);
            
            // Check external service availability
            if (!IsExternalServiceAvailable()) {
                // Can wait for service to become available
                ctx.Event.SetData("waiting_for", "external_service");
                return HandlerProcessResult.WAITING;
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

### Execute Phase

**Purpose**: Core business logic and main event processing
**Behavior**: Failures don't stop other handlers, continues to cleanup
**Waiting Support**: Can pause for async operations

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            try {
                // Core business logic
                var userId = CreateUserInDatabase(ctx.Event);
                ctx.Event.SetData("user_id", userId);
                
                // Async operation example
                if (RequiresEmailVerification(ctx.Event)) {
                    SendVerificationEmail(ctx.Event);
                    ctx.Event.SetData("waiting_for", "email_verification");
                    return HandlerProcessResult.WAITING;
                }
                
                return HandlerProcessResult.SUCCESS;
            } catch (DatabaseException ex) {
                ctx.Event.SetData("database_error", ex.Message);
                return HandlerProcessResult.FAILURE; // Continue to other handlers
            }
        })
        .Build());
```

### Cleanup Phase

**Purpose**: Cleanup, finalization, resource disposal
**Behavior**: Always executes, failures don't stop other cleanup handlers
**Best Practice**: Should never fail

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.CleanupPhaseState>(
    register => register
        .Handler(ctx => {
            // Release resources
            if (ctx.Event.TryGetData("db_connection", out var connection)) {
                ReleaseConnection(connection);
            }
            
            // Log completion
            LogRegistrationEvent(ctx.Event);
            
            // Update metrics
            UpdateRegistrationMetrics(ctx.Event);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

## State Management

### BusinessState

The primary processing state that manages all phases:

```csharp
// BusinessState automatically handles:
// - Sequential phase execution
// - Phase result evaluation
// - Transition to success/failure states
// - Waiting state management
```

### SucceedState

Handles post-success processing and cleanup:

```csharp
dispatcher.ForEventState<UserRegistrationEvent, SucceedState>(
    register => register
        .Handler(evt => {
            // Success-only processing
            var userId = evt.GetData<int>("user_id");
            SendWelcomeEmail(evt.GetData<string>("email"));
            NotifyAdministrators(userId);
        })
        .Build());
```

### CancelledState

Handles cancellation cleanup:

```csharp
dispatcher.ForEventState<UserRegistrationEvent, CancelledState>(
    register => register
        .Handler(evt => {
            // Cancellation cleanup
            var reason = evt.GetData<string>("cancellation_reason");
            LogCancellation(evt.ID, reason);
            NotifyUserOfCancellation(evt.GetData<string>("email"));
        })
        .Build());
```

### CompletedState

Final state for all events:

```csharp
dispatcher.ForEventState<UserRegistrationEvent, CompletedState>(
    register => register
        .Handler(evt => {
            // Final cleanup and logging
            var duration = DateTime.UtcNow - evt.CreatedAt;
            RecordProcessingTime(evt.ID, duration);
            FinalizeAuditTrail(evt);
        })
        .Build());
```

## Error Handling

### Exception Handling Strategy

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            try {
                // Risky operation
                var result = CallExternalService(ctx.Event);
                return HandlerProcessResult.SUCCESS;
                
            } catch (SecurityException ex) {
                // Critical security issue - cancel immediately
                ctx.Event.SetData("security_error", ex.Message);
                return HandlerProcessResult.CANCELLED;
                
            } catch (ValidationException ex) {
                // Recoverable validation error - mark as failed but continue
                ctx.Event.SetData("validation_error", ex.Message);
                return HandlerProcessResult.FAILURE;
                
            } catch (TransientException ex) {
                // Temporary issue - might want to wait and retry
                ctx.Event.SetData("retry_reason", ex.Message);
                return HandlerProcessResult.WAITING;
                
            } catch (Exception ex) {
                // Unexpected error - log and fail gracefully
                LogUnexpectedError(ctx.Event, ex);
                ctx.Event.SetData("unexpected_error", ex.Message);
                return HandlerProcessResult.FAILURE;
            }
        })
        .Build());
```

### Failure Recovery Patterns

```csharp
// Circuit breaker pattern
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var failureCount = GetServiceFailureCount();
            if (failureCount > CIRCUIT_BREAKER_THRESHOLD) {
                ctx.Event.SetData("circuit_breaker", "open");
                return HandlerProcessResult.FAILURE;
            }
            
            // Proceed with service call
            return CallServiceWithFailureTracking(ctx);
        })
        .Build());

// Retry pattern with exponential backoff
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var retryCount = ctx.Event.GetData<int>("retry_count");
            if (retryCount >= MAX_RETRIES) {
                return HandlerProcessResult.FAILURE;
            }
            
            try {
                return CallExternalService(ctx);
            } catch (TransientException) {
                ctx.Event.SetData("retry_count", retryCount + 1);
                var delay = CalculateExponentialBackoff(retryCount);
                ScheduleRetry(ctx, delay);
                return HandlerProcessResult.WAITING;
            }
        })
        .Build());
```

## Asynchronous Operations

### Basic Waiting Pattern

```csharp
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            // Initiate async operation
            var operationId = StartAsyncOperation(ctx.Event);
            ctx.Event.SetData("async_operation_id", operationId);
            
            // Register callback for operation completion
            RegisterOperationCallback(operationId, () => {
                // When operation completes, resume the event
                var context = GetEventContext(ctx.Event.ID);
                context.Resume(); // or context.Fail() or context.Cancel()
            });
            
            return HandlerProcessResult.WAITING;
        })
        .Build());
```

### External Service Integration

```csharp
// Email verification example
public class EmailVerificationService {
    private readonly Dictionary<Guid, EventSystemContext> _waitingEvents = new();
    
    public void SendVerificationEmail(ILSEvent evt, EventSystemContext context) {
        var verificationId = Guid.NewGuid();
        evt.SetData("verification_id", verificationId);
        
        // Store context for later resumption
        _waitingEvents[verificationId] = context;
        
        // Send email with verification link
        var email = evt.GetData<string>("email");
        var verificationLink = GenerateVerificationLink(verificationId);
        SendEmail(email, verificationLink);
    }
    
    // Called when user clicks verification link
    public void HandleVerificationClick(Guid verificationId) {
        if (_waitingEvents.TryGetValue(verificationId, out var context)) {
            _waitingEvents.Remove(verificationId);
            context.Event.SetData("email_verified", true);
            context.Resume(); // Continue event processing
        }
    }
    
    // Called on verification timeout
    public void HandleVerificationTimeout(Guid verificationId) {
        if (_waitingEvents.TryGetValue(verificationId, out var context)) {
            _waitingEvents.Remove(verificationId);
            context.Event.SetData("verification_timeout", true);
            context.Fail(); // Mark as failed but continue processing
        }
    }
}
```

### Integration Example

```csharp
private readonly EmailVerificationService _emailService = new();

dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            // Create user account first
            var userId = CreateUserAccount(ctx.Event);
            ctx.Event.SetData("user_id", userId);
            
            // Check if email verification is required
            var requiresVerification = ctx.Event.GetData<bool>("requires_email_verification");
            if (requiresVerification) {
                _emailService.SendVerificationEmail(ctx.Event, ctx);
                return HandlerProcessResult.WAITING; // Pause processing
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

## Best Practices

### 1. Event Design

```csharp
// ✅ Good: Immutable event with clear data
public class UserRegistrationEvent : BaseEvent {
    public string Email { get; }
    public string Name { get; }
    public DateTime RegistrationTime { get; }
    
    public UserRegistrationEvent(LSESDispatcher dispatcher, string email, string name)
        : base(dispatcher) {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        RegistrationTime = DateTime.UtcNow;
        
        // Store as event data for handler access
        SetData("email", email);
        SetData("name", name);
        SetData("registration_time", RegistrationTime);
    }
}

// ❌ Bad: Mutable properties, missing validation
public class BadUserRegistrationEvent : BaseEvent {
    public string Email { get; set; } // Mutable!
    public string Name { get; set; }   // Mutable!
}
```

### 2. Handler Organization

```csharp
// ✅ Good: Focused, single-responsibility handlers
public static class UserValidationHandlers {
    public static HandlerProcessResult ValidateEmailFormat(EventSystemContext ctx) {
        var email = ctx.Event.GetData<string>("email");
        if (!IsValidEmail(email)) {
            ctx.Event.SetData("validation_error", "Invalid email format");
            return HandlerProcessResult.FAILURE;
        }
        return HandlerProcessResult.SUCCESS;
    }
    
    public static HandlerProcessResult CheckEmailAvailability(EventSystemContext ctx) {
        var email = ctx.Event.GetData<string>("email");
        if (IsEmailTaken(email)) {
            ctx.Event.SetData("validation_error", "Email already registered");
            return HandlerProcessResult.FAILURE;
        }
        return HandlerProcessResult.SUCCESS;
    }
}

// Registration
dispatcher.ForEvent<UserRegistrationEvent>(register => register
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.HIGH)
        .Handler(UserValidationHandlers.ValidateEmailFormat)
        .Build())
    .OnPhase<BusinessState.ValidatePhaseState>(phase => phase
        .WithPriority(LSESPriority.NORMAL)
        .Handler(UserValidationHandlers.CheckEmailAvailability)
        .Build())
    .Register());
```

### 3. Error Handling

```csharp
// ✅ Good: Comprehensive error handling with context
dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            try {
                var result = CreateUserAccount(ctx.Event);
                ctx.Event.SetData("user_id", result.UserId);
                return HandlerProcessResult.SUCCESS;
                
            } catch (DuplicateEmailException ex) {
                // Business logic error - recoverable
                ctx.Event.SetData("error_type", "duplicate_email");
                ctx.Event.SetData("error_message", ex.Message);
                return HandlerProcessResult.FAILURE;
                
            } catch (DatabaseConnectionException ex) {
                // Infrastructure error - might be transient
                ctx.Event.SetData("error_type", "database_connection");
                ctx.Event.SetData("error_message", ex.Message);
                LogError("Database connection failed", ex, ctx.Event);
                return HandlerProcessResult.WAITING; // Retry later
                
            } catch (SecurityException ex) {
                // Security violation - critical
                ctx.Event.SetData("error_type", "security_violation");
                ctx.Event.SetData("error_message", ex.Message);
                LogSecurityEvent("Registration security violation", ex, ctx.Event);
                return HandlerProcessResult.CANCELLED;
            }
        })
        .Build());
```

### 4. Data Management

```csharp
// ✅ Good: Consistent data access patterns
public static class EventDataKeys {
    public const string EMAIL = "email";
    public const string USER_ID = "user_id";
    public const string VALIDATION_ERRORS = "validation_errors";
    public const string PROCESSING_START_TIME = "processing_start_time";
}

dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
    register => register
        .Handler(ctx => {
            // Use constants and safe access
            if (!ctx.Event.TryGetData(EventDataKeys.EMAIL, out string email)) {
                LogError("Email data missing from event", ctx.Event);
                return HandlerProcessResult.FAILURE;
            }
            
            var errors = ctx.Event.GetData<List<string>>(EventDataKeys.VALIDATION_ERRORS) 
                         ?? new List<string>();
                         
            if (!IsValidEmail(email)) {
                errors.Add("Invalid email format");
                ctx.Event.SetData(EventDataKeys.VALIDATION_ERRORS, errors);
                return HandlerProcessResult.FAILURE;
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

### 5. Testing

```csharp
[Test]
public void UserRegistration_WithValidData_ShouldSucceed() {
    // Arrange
    var dispatcher = new LSESDispatcher();
    var testEmail = "test@example.com";
    var testName = "Test User";
    
    // Register test handlers
    dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
        register => register
            .Handler(ctx => {
                // Mock validation logic
                return HandlerProcessResult.SUCCESS;
            })
            .Build());
    
    dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ExecutePhaseState>(
        register => register
            .Handler(ctx => {
                // Mock user creation
                ctx.Event.SetData("user_id", 123);
                return HandlerProcessResult.SUCCESS;
            })
            .Build());
    
    // Act
    var registrationEvent = new UserRegistrationEvent(dispatcher, testEmail, testName);
    var result = registrationEvent.Dispatch();
    
    // Assert
    Assert.AreEqual(EventProcessResult.SUCCESS, result);
    Assert.IsTrue(registrationEvent.IsCompleted);
    Assert.IsFalse(registrationEvent.HasFailures);
    Assert.IsFalse(registrationEvent.IsCancelled);
    Assert.AreEqual(123, registrationEvent.GetData<int>("user_id"));
}

[Test]
public void UserRegistration_WithInvalidEmail_ShouldFail() {
    // Arrange
    var dispatcher = new LSESDispatcher();
    var invalidEmail = "not-an-email";
    var testName = "Test User";
    
    dispatcher.ForEventPhase<UserRegistrationEvent, BusinessState.ValidatePhaseState>(
        register => register
            .Handler(ctx => {
                var email = ctx.Event.GetData<string>("email");
                if (!email.Contains("@")) {
                    ctx.Event.SetData("error", "Invalid email format");
                    return HandlerProcessResult.FAILURE;
                }
                return HandlerProcessResult.SUCCESS;
            })
            .Build());
    
    // Act
    var registrationEvent = new UserRegistrationEvent(dispatcher, invalidEmail, testName);
    var result = registrationEvent.Dispatch();
    
    // Assert
    Assert.AreEqual(EventProcessResult.FAILURE, result);
    Assert.IsTrue(registrationEvent.IsCompleted);
    Assert.IsTrue(registrationEvent.HasFailures);
    Assert.IsFalse(registrationEvent.IsCancelled);
    Assert.AreEqual("Invalid email format", registrationEvent.GetData<string>("error"));
}
```

## Examples

### E-commerce Order Processing

```csharp
public class OrderProcessingEvent : BaseEvent {
    public int OrderId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public int CustomerId { get; }
    
    public OrderProcessingEvent(LSESDispatcher dispatcher, int orderId, decimal amount, string currency, int customerId)
        : base(dispatcher) {
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
        CustomerId = customerId;
        
        SetData("order_id", orderId);
        SetData("amount", amount);
        SetData("currency", currency);
        SetData("customer_id", customerId);
    }
}

// Register handlers
var dispatcher = LSESDispatcher.Singleton;

// Validation phase
dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSESPriority.CRITICAL)
        .Handler(ctx => {
            // Validate customer
            var customerId = ctx.Event.GetData<int>("customer_id");
            if (!IsValidCustomer(customerId)) {
                ctx.Event.SetData("error", "Invalid customer");
                return HandlerProcessResult.CANCELLED;
            }
            
            // Validate amount
            var amount = ctx.Event.GetData<decimal>("amount");
            if (amount <= 0) {
                ctx.Event.SetData("error", "Invalid amount");
                return HandlerProcessResult.FAILURE;
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Configuration phase
dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.ConfigurePhaseState>(
    register => register
        .Handler(ctx => {
            // Reserve inventory
            var orderId = ctx.Event.GetData<int>("order_id");
            var reservationId = ReserveInventory(orderId);
            ctx.Event.SetData("reservation_id", reservationId);
            
            // Calculate tax
            var amount = ctx.Event.GetData<decimal>("amount");
            var customerId = ctx.Event.GetData<int>("customer_id");
            var tax = CalculateTax(amount, customerId);
            ctx.Event.SetData("tax_amount", tax);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Execution phase - payment processing
dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var amount = ctx.Event.GetData<decimal>("amount");
            var tax = ctx.Event.GetData<decimal>("tax_amount");
            var customerId = ctx.Event.GetData<int>("customer_id");
            
            // Process payment asynchronously
            var paymentId = InitiatePaymentProcessing(customerId, amount + tax);
            ctx.Event.SetData("payment_id", paymentId);
            
            // Register payment callback
            RegisterPaymentCallback(paymentId, (success, transactionId) => {
                var context = GetEventContext(ctx.Event.ID);
                if (success) {
                    ctx.Event.SetData("transaction_id", transactionId);
                    context.Resume();
                } else {
                    ctx.Event.SetData("payment_error", "Payment failed");
                    context.Fail();
                }
            });
            
            return HandlerProcessResult.WAITING;
        })
        .Build());

// Cleanup phase
dispatcher.ForEventPhase<OrderProcessingEvent, BusinessState.CleanupPhaseState>(
    register => register
        .Handler(ctx => {
            // Release inventory reservation if payment failed
            if (ctx.Event.HasFailures && ctx.Event.TryGetData("reservation_id", out int reservationId)) {
                ReleaseInventoryReservation(reservationId);
            }
            
            // Log order processing
            LogOrderProcessing(ctx.Event);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Success handlers
dispatcher.ForEventState<OrderProcessingEvent, SucceedState>(
    register => register
        .Handler(evt => {
            // Send confirmation email
            var customerId = evt.GetData<int>("customer_id");
            var orderId = evt.GetData<int>("order_id");
            SendOrderConfirmation(customerId, orderId);
            
            // Update analytics
            UpdateOrderAnalytics(evt);
        })
        .Build());

// Usage
var orderEvent = new OrderProcessingEvent(dispatcher, 12345, 99.99m, "USD", 67890);
var result = orderEvent.Dispatch();

if (result == EventProcessResult.SUCCESS) {
    var transactionId = orderEvent.GetData<string>("transaction_id");
    Console.WriteLine($"Order processed successfully. Transaction: {transactionId}");
}
```

### File Processing Pipeline

```csharp
public class FileProcessingEvent : BaseEvent {
    public string FilePath { get; }
    public string ProcessingType { get; }
    
    public FileProcessingEvent(LSESDispatcher dispatcher, string filePath, string processingType)
        : base(dispatcher) {
        FilePath = filePath;
        ProcessingType = processingType;
        
        SetData("file_path", filePath);
        SetData("processing_type", processingType);
        SetData("start_time", DateTime.UtcNow);
    }
}

// File validation
dispatcher.ForEventPhase<FileProcessingEvent, BusinessState.ValidatePhaseState>(
    register => register
        .Handler(ctx => {
            var filePath = ctx.Event.GetData<string>("file_path");
            
            // Check file exists
            if (!File.Exists(filePath)) {
                ctx.Event.SetData("error", "File not found");
                return HandlerProcessResult.FAILURE;
            }
            
            // Check file size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MAX_FILE_SIZE) {
                ctx.Event.SetData("error", "File too large");
                return HandlerProcessResult.FAILURE;
            }
            
            ctx.Event.SetData("file_size", fileInfo.Length);
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Resource allocation
dispatcher.ForEventPhase<FileProcessingEvent, BusinessState.ConfigurePhaseState>(
    register => register
        .Handler(ctx => {
            // Allocate processing resources
            var processingType = ctx.Event.GetData<string>("processing_type");
            var resourceId = AllocateProcessingResources(processingType);
            ctx.Event.SetData("resource_id", resourceId);
            
            // Create temporary working directory
            var workDir = CreateTemporaryDirectory();
            ctx.Event.SetData("work_directory", workDir);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// File processing
dispatcher.ForEventPhase<FileProcessingEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var filePath = ctx.Event.GetData<string>("file_path");
            var processingType = ctx.Event.GetData<string>("processing_type");
            var workDir = ctx.Event.GetData<string>("work_directory");
            var resourceId = ctx.Event.GetData<string>("resource_id");
            
            // Start async processing
            var processingJobId = StartAsyncFileProcessing(filePath, processingType, workDir, resourceId);
            ctx.Event.SetData("processing_job_id", processingJobId);
            
            // Register completion callback
            RegisterProcessingCallback(processingJobId, (success, outputPath, error) => {
                var context = GetEventContext(ctx.Event.ID);
                if (success) {
                    ctx.Event.SetData("output_path", outputPath);
                    context.Resume();
                } else {
                    ctx.Event.SetData("processing_error", error);
                    context.Fail();
                }
            });
            
            return HandlerProcessResult.WAITING;
        })
        .Build());

// Cleanup
dispatcher.ForEventPhase<FileProcessingEvent, BusinessState.CleanupPhaseState>(
    register => register
        .Handler(ctx => {
            // Release processing resources
            if (ctx.Event.TryGetData("resource_id", out string resourceId)) {
                ReleaseProcessingResources(resourceId);
            }
            
            // Cleanup temporary directory
            if (ctx.Event.TryGetData("work_directory", out string workDir)) {
                Directory.Delete(workDir, true);
            }
            
            // Calculate processing time
            var startTime = ctx.Event.GetData<DateTime>("start_time");
            var duration = DateTime.UtcNow - startTime;
            ctx.Event.SetData("processing_duration", duration);
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Usage with event-scoped handlers for specific file types
var fileEvent = new FileProcessingEvent(dispatcher, "document.pdf", "pdf_extraction")
    .WithPhaseCallbacks<BusinessState.ValidatePhaseState>(
        // PDF-specific validation
        register => register
            .Handler(ctx => {
                var filePath = ctx.Event.GetData<string>("file_path");
                if (!IsPdfFile(filePath)) {
                    ctx.Event.SetData("error", "Not a valid PDF file");
                    return HandlerProcessResult.FAILURE;
                }
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    );

var result = fileEvent.Dispatch();
```

## API Reference

### Core Interfaces

#### ILSEvent

```csharp
public interface ILSEvent {
    Guid ID { get; }
    DateTime CreatedAt { get; }
    bool IsCancelled { get; }
    bool HasFailures { get; }
    bool IsCompleted { get; }
    bool InDispatch { get; }
    IReadOnlyDictionary<string, object> Data { get; }
    
    void SetData<T>(string key, T value);
    T GetData<T>(string key);
    bool TryGetData<T>(string key, out T value);
    EventProcessResult Dispatch();
}
```

#### IEventSystemState

```csharp
public interface IEventSystemState {
    StateProcessResult StateResult { get; }
    bool HasFailures { get; }
    bool HasCancelled { get; }
    
    IEventSystemState? Process();
    IEventSystemState? Resume();
    IEventSystemState? Cancel();
    IEventSystemState? Fail();
}
```

#### IHandlerEntry

```csharp
public interface IHandlerEntry {
    Guid ID { get; }
    LSESPriority Priority { get; }
    Func<ILSEvent, IHandlerEntry, bool> Condition { get; }
    int ExecutionCount { get; }
}
```

### Core Classes

#### LSEvent

```csharp
public abstract class BaseEvent : ILSEvent {
    public Guid ID { get; }
    public DateTime CreatedAt { get; }
    public bool IsCancelled { get; }
    public bool HasFailures { get; }
    public bool IsCompleted { get; }
    public bool InDispatch { get; }
    public IReadOnlyDictionary<string, object> Data { get; }
    
    protected BaseEvent(LSESDispatcher dispatcher);
    
    public virtual void SetData<T>(string key, T value);
    public virtual T GetData<T>(string key);
    public virtual bool TryGetData<T>(string key, out T value);
    public EventProcessResult Dispatch();
    public ILSEvent WithPhaseCallbacks<TPhase>(params Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry>[] configure) where TPhase : BusinessState.PhaseState;
}
```

#### LSDispatcher

```csharp
public class LSESDispatcher {
    public static LSESDispatcher Singleton { get; }
    
    public Guid[] ForEvent<TEvent>(Func<EventSystemRegister<TEvent>, Guid[]> configureRegister) where TEvent : ILSEvent;
    public Guid ForEventPhase<TEvent, TPhase>(Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry> configurePhaseHandler) where TEvent : ILSEvent where TPhase : BusinessState.PhaseState;
    public Guid ForEventState<TEvent, TState>(Func<StateHandlerRegister<TState>, StateHandlerEntry> configureStateHandler) where TEvent : ILSEvent where TState : IEventSystemState;
}
```

#### LSEventProcessContext

```csharp
public class EventSystemContext {
    public LSESDispatcher Dispatcher { get; }
    public IEventSystemState? CurrentState { get; }
    public ILSEvent Event { get; }
    public IReadOnlyList<IHandlerEntry> Handlers { get; }
    public bool HasFailures { get; }
    public bool IsCancelled { get; }
    
    public IEventSystemState? Resume();
    public IEventSystemState? Cancel();
    public IEventSystemState? Fail();
}
```

### Enumerations

#### EventProcessResult

```csharp
public enum EventProcessResult {
    UNKNOWN,
    SUCCESS,
    FAILURE,
    CANCELLED,
    WAITING
}
```

#### HandlerProcessResult

```csharp
public enum HandlerProcessResult {
    UNKNOWN,
    SUCCESS,
    FAILURE,
    CANCELLED,
    WAITING
}
```

#### StateProcessResult

```csharp
public enum StateProcessResult {
    UNKNOWN,
    SUCCESS,
    FAILURE,
    WAITING,
    CANCELLED
}
```

#### PhaseProcessResult

```csharp
public enum PhaseProcessResult {
    UNKNOWN,
    CONTINUE,
    FAILURE,
    WAITING,
    CANCELLED
}
```

#### LSESPriority

```csharp
public enum LSESPriority {
    BACKGROUND = 0,
    LOW = 1,
    NORMAL = 2,
    HIGH = 3,
    CRITICAL = 4
}
```

### Registration Builders

#### EventSystemRegister\<TEvent\>

```csharp
public class EventSystemRegister<TEvent> where TEvent : ILSEvent {
    public EventSystemRegister<TEvent> OnPhase<TPhase>(Func<PhaseHandlerRegister<TPhase>, PhaseHandlerEntry> configurePhaseHandler) where TPhase : BusinessState.PhaseState;
    public EventSystemRegister<TEvent> OnState<TState>(Func<StateHandlerRegister<TState>, StateHandlerEntry> configureStateHandler) where TState : IEventSystemState;
    public Guid[] Register();
}
```

#### PhaseHandlerRegister\<TPhase\>

```csharp
public class PhaseHandlerRegister<TPhase> where TPhase : BusinessState.PhaseState {
    public PhaseHandlerRegister<TPhase> WithPriority(LSESPriority priority);
    public PhaseHandlerRegister<TPhase> When(Func<ILSEvent, IHandlerEntry, bool> condition);
    public PhaseHandlerRegister<TPhase> Handler(Func<EventSystemContext, HandlerProcessResult> handler);
    public PhaseHandlerEntry Build();
}
```

#### StateHandlerRegister\<TState\>

```csharp
public class StateHandlerRegister<TState> where TState : IEventSystemState {
    public StateHandlerRegister<TState> WithPriority(LSESPriority priority);
    public StateHandlerRegister<TState> When(Func<ILSEvent, IHandlerEntry, bool> condition);
    public StateHandlerRegister<TState> Handler(LSAction<ILSEvent> handler);
    public StateHandlerEntry Build();
}
```

## Migration Guide

### From v3 to v4

The v4 redesign introduces significant architectural changes focused on simplification and clarity:

#### Key Changes

1. **Simplified Phase Model**: Reduced from 7 phases to 4 clean phases
2. **State Machine Architecture**: Clear state-based processing with defined transitions
3. **Unified Handler Registration**: Single dispatcher with fluent API
4. **Improved Error Handling**: Distinction between failures and cancellations
5. **Better Async Support**: Explicit waiting states with external control

## Performance Considerations

### Memory Management

1. **Event Data Storage**: Use appropriate data types and avoid storing large objects in event data
2. **Handler Registration**: Global handlers are reused; event-scoped handlers are created per event
3. **State Machine**: Minimal overhead with proper state transitions

### Scalability

1. **Handler Count**: System scales well with reasonable handler counts per event type
2. **Concurrent Events**: Each event processes independently with thread-safe data access
3. **Async Operations**: External control prevents thread pool exhaustion

### Optimization Tips

1. **Use Conditional Handlers**: Reduce unnecessary handler execution with When() clauses
2. **Proper Priority**: Use appropriate priorities to optimize execution order
3. **Efficient Data Access**: Use TryGetData() for optional data to avoid exceptions
4. **Resource Cleanup**: Always implement proper cleanup in CleanupPhaseState

## Troubleshooting

### Common Issues

#### 1. Event Not Processing

```csharp
// Check if dispatcher is properly injected
var event = new MyEvent(LSESDispatcher.Singleton); // ✅ Correct

// Check if handlers are registered
var handlerIds = dispatcher.ForEventPhase<MyEvent, BusinessState.ValidatePhaseState>(...);
// Verify handlerIds is not empty
```

#### 2. Handlers Not Executing

```csharp
// Check handler conditions
.When((evt, entry) => {
    // Ensure condition returns true when handler should execute
    return evt.GetData<string>("type") == "expected_type";
})

// Check priorities - higher priority (CRITICAL) executes before lower (BACKGROUND)
```

#### 3. Event Stuck in Waiting State

```csharp
// Ensure external actor calls Resume(), Cancel(), or Fail()
var context = GetEventContext(eventId);
context.Resume(); // Continue processing

// Check for proper callback registration in async operations
RegisterCallback(operationId, () => {
    context.Resume(); // ✅ Must call this
});
```

#### 4. Data Not Available

```csharp
// Use safe data access
if (event.TryGetData("key", out string value)) {
    // Use value safely
} else {
    // Handle missing data
}

// Check data key consistency
public static class DataKeys {
    public const string EMAIL = "email"; // Use constants
}
```

### Debugging

#### Enable Detailed Logging

```csharp
dispatcher.ForEventPhase<MyEvent, BusinessState.ValidatePhaseState>(
    register => register
        .Handler(ctx => {
            // Add logging
            Console.WriteLine($"Processing event {ctx.Event.ID} in validate phase");
            
            // Log data
            foreach (var kvp in ctx.Event.Data) {
                Console.WriteLine($"Data: {kvp.Key} = {kvp.Value}");
            }
            
            return HandlerProcessResult.SUCCESS;
        })
        .Build());
```

#### Track Handler Execution

```csharp
dispatcher.ForEventPhase<MyEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var stopwatch = Stopwatch.StartNew();
            
            try {
                // Handler logic
                var result = ProcessEvent(ctx);
                
                stopwatch.Stop();
                Console.WriteLine($"Handler executed in {stopwatch.ElapsedMilliseconds}ms");
                
                return result;
            } catch (Exception ex) {
                stopwatch.Stop();
                Console.WriteLine($"Handler failed after {stopwatch.ElapsedMilliseconds}ms: {ex}");
                throw;
            }
        })
        .Build());
```

### Performance Monitoring

```csharp
public class EventPerformanceMonitor {
    private static readonly ConcurrentDictionary<Guid, DateTime> _eventStartTimes = new();
    
    public static void TrackEventStart(ILSEvent evt) {
        _eventStartTimes[evt.ID] = DateTime.UtcNow;
    }
    
    public static void TrackEventComplete(ILSEvent evt) {
        if (_eventStartTimes.TryRemove(evt.ID, out var startTime)) {
            var duration = DateTime.UtcNow - startTime;
            Console.WriteLine($"Event {evt.ID} completed in {duration.TotalMilliseconds}ms");
            
            // Record metrics
            RecordEventMetrics(evt.GetType().Name, duration, evt.HasFailures, evt.IsCancelled);
        }
    }
}

// Use in event-scoped handlers
var event = new MyEvent(dispatcher)
    .WithPhaseCallbacks<BusinessState.ValidatePhaseState>(
        register => register
            .WithPriority(LSESPriority.BACKGROUND)
            .Handler(ctx => {
                EventPerformanceMonitor.TrackEventStart(ctx.Event);
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    )
    .WithPhaseCallbacks<BusinessState.CleanupPhaseState>(
        register => register
            .WithPriority(LSESPriority.BACKGROUND)
            .Handler(ctx => {
                EventPerformanceMonitor.TrackEventComplete(ctx.Event);
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    );
```

---

## Conclusion

LSEventSystem v4 provides a robust, clean, and scalable foundation for event-driven applications. Its state-machine architecture, comprehensive error handling, and flexible handler registration make it suitable for a wide range of use cases from simple business logic to complex asynchronous workflows.

The framework's emphasis on immutability, type safety, and clear separation of concerns ensures maintainable and testable code while providing the flexibility needed for real-world applications.

For additional support, examples, or contributions, please refer to the project repository and documentation.
