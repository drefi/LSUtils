# LSDispatcher Phase-Based System Migration

## Overview

This document outlines the migration from the current callback-based event system to a more structured phase-based approach that provides better control flow, cleaner code, and predictable execution order.

## What's New

### 1. Event Phases
Events now progress through well-defined phases:
- **DISPATCH**: Initial notification when event starts
- **PRE_EXECUTION**: Validation, security checks, setup
- **EXECUTION**: Main business logic (default phase)
- **POST_EXECUTION**: Secondary effects, logging
- **SUCCESS**: Success-specific operations (conditional)
- **FAILURE**: Failure-specific operations (conditional)  
- **CANCEL**: Cancellation-specific operations (conditional)
- **COMPLETE**: Final cleanup (always executed)

### 2. Status-Based Listeners
Instead of manually calling `event.Signal()`, `event.Failure()`, or `event.Cancel()`, listeners can now return status codes:

```csharp
// OLD WAY
dispatcher.Register<MyEvent>((id, evt) => {
    if (ValidateData(evt)) {
        ProcessData(evt);
        evt.Signal(); // Must remember to signal
    } else {
        evt.Failure("Validation failed"); // Manual failure
    }
});

// NEW WAY  
dispatcher.RegisterStatus<MyEvent>((id, evt) => {
    if (!ValidateData(evt)) {
        return EventProcessingStatus.FAILURE; // Clean and declarative
    }
    
    ProcessData(evt);
    return EventProcessingStatus.SUCCESS; // Automatic signaling
}, phase: EventPhase.EXECUTION);
```

### 3. Priority Control
Within each phase, listeners can have different priorities:
- `PhasePriority.CRITICAL` (-100) - Executes first
- `PhasePriority.HIGH` (-50)
- `PhasePriority.NORMAL` (0) - Default
- `PhasePriority.LOW` (50)
- `PhasePriority.MINIMAL` (100) - Executes last

## Migration Strategy

### Immediate Benefits (No Code Changes Required)
- **100% Backward Compatibility**: All existing code continues to work unchanged
- **Default Phase**: Existing listeners automatically use `EventPhase.EXECUTION`
- **Existing APIs**: All current `Register()` methods remain functional

### Gradual Migration Path

#### Phase 1: Start Using New Registration Methods
```csharp
// Add phase information to new listeners
dispatcher.Register<MyEvent>((id, evt) => {
    // Your existing logic
    evt.Signal();
}, phase: EventPhase.PRE_EXECUTION, priority: PhasePriority.HIGH);
```

#### Phase 2: Convert to Status-Based Listeners
```csharp
// Convert existing listeners to status-based
dispatcher.RegisterStatus<MyEvent>((id, evt) => {
    // Your existing logic (remove Signal/Failure/Cancel calls)
    return EventProcessingStatus.SUCCESS;
}, phase: EventPhase.EXECUTION);
```

#### Phase 3: Leverage Phase-Specific Logic
```csharp
// Split complex listeners into appropriate phases
dispatcher.RegisterStatus<BusinessTransactionEvent>((id, evt) => {
    return SecurityService.ValidateUser(evt.User) 
        ? EventProcessingStatus.SUCCESS 
        : EventProcessingStatus.CANCEL;
}, phase: EventPhase.PRE_EXECUTION, priority: PhasePriority.CRITICAL);

dispatcher.RegisterStatus<BusinessTransactionEvent>((id, evt) => {
    return PaymentService.ProcessPayment(evt.Payment)
        ? EventProcessingStatus.SUCCESS 
        : EventProcessingStatus.FAILURE;
}, phase: EventPhase.EXECUTION);

dispatcher.RegisterStatus<BusinessTransactionEvent>((id, evt) => {
    EmailService.SendConfirmation(evt.User);
    return EventProcessingStatus.SUCCESS;
}, phase: EventPhase.SUCCESS);
```

## New Registration Methods

### Phase-Based Registration
```csharp
// Register with phase and priority
dispatcher.Register<MyEvent>(listener, 
    phase: EventPhase.PRE_EXECUTION, 
    priority: PhasePriority.HIGH);
```

### Status-Based Registration
```csharp
// Simple status return
dispatcher.RegisterStatus<MyEvent>((id, evt) => {
    // Process event
    return EventProcessingStatus.SUCCESS;
});

// Status with message
dispatcher.RegisterStatus<MyEvent>((id, evt, out string? message) => {
    message = null;
    try {
        ProcessEvent(evt);
        message = "Processing completed";
        return EventProcessingStatus.SUCCESS;
    } catch (Exception ex) {
        message = $"Error: {ex.Message}";
        return EventProcessingStatus.FAILURE;
    }
});
```

## Event Processing Status Codes

| Status | Description | Equivalent Action |
|--------|-------------|-------------------|
| `SUCCESS` | Listener completed successfully | `event.Signal()` |
| `FAILURE` | Listener encountered an error | `event.Failure(message)` |
| `CANCEL` | Listener requests cancellation | `event.Cancel()` |
| `RUNNING` | Async operation in progress | `event.Wait()` |
| `SKIP` | Listener chose not to process | No action |

## Best Practices

### 1. Use Appropriate Phases
- **PRE_EXECUTION**: Input validation, security checks
- **EXECUTION**: Main business logic
- **POST_EXECUTION**: Side effects, logging
- **SUCCESS/FAILURE/CANCEL**: Conditional cleanup and notifications
- **COMPLETE**: Final resource cleanup

### 2. Set Proper Priorities
- **CRITICAL**: Security, validation that must run first
- **HIGH**: Important setup operations
- **NORMAL**: Standard business logic
- **LOW**: Notifications, logging
- **MINIMAL**: Final cleanup operations

### 3. Status-Based Flow Control
```csharp
// Validation phase
dispatcher.RegisterStatus<UserRegistrationEvent>((id, evt) => {
    if (!IsValidEmail(evt.Email)) return EventProcessingStatus.FAILURE;
    if (UserExists(evt.Email)) return EventProcessingStatus.CANCEL;
    return EventProcessingStatus.SUCCESS;
}, phase: EventPhase.PRE_EXECUTION);

// Main processing
dispatcher.RegisterStatus<UserRegistrationEvent>((id, evt) => {
    CreateUser(evt);
    return EventProcessingStatus.SUCCESS;
}, phase: EventPhase.EXECUTION);

// Success notifications
dispatcher.RegisterStatus<UserRegistrationEvent>((id, evt) => {
    SendWelcomeEmail(evt.User);
    return EventProcessingStatus.SUCCESS;
}, phase: EventPhase.SUCCESS);
```

### 4. Async Processing
```csharp
dispatcher.RegisterStatus<FileUploadEvent>((id, evt) => {
    // Start async upload
    UploadFileAsync(evt.File, (success, error) => {
        if (success) evt.Signal();
        else evt.Failure(error);
    });
    
    return EventProcessingStatus.RUNNING; // Still processing
});
```

## Benefits of Migration

### 1. **Cleaner Code**
- No need to remember manual signaling
- Self-documenting status returns
- Reduced boilerplate code

### 2. **Predictable Flow**
- Clear execution order through phases
- Structured error handling
- Automatic conditional logic

### 3. **Better Error Handling**
- Dedicated failure phase for cleanup
- Structured cancellation handling
- Guaranteed completion phase

### 4. **Easier Testing**
- Phase-specific testing
- Predictable execution order
- Clear separation of concerns

### 5. **Enhanced Debugging**
- Clear phase progression tracking
- Better error context
- Easier to trace execution flow

## Migration Timeline

1. **Week 1-2**: Familiarize team with new concepts
2. **Week 3-4**: Start using phase-based registration for new features
3. **Week 5-8**: Gradually convert existing listeners to status-based approach
4. **Week 9-12**: Refactor complex event flows to use appropriate phases
5. **Ongoing**: Use new system for all new development

The migration can be done incrementally without any breaking changes to existing functionality.
