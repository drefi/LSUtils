# LSProcessSystem Quick Reference Guide

## Overview

The LSProcessSystem provides a flexible, tree-based processing framework that supports complex business logic through composable nodes. It's designed for scenarios requiring sequential, parallel, or conditional execution of operations with waiting/resumption capabilities.

## LSProcess - The Core Processing Entity

### Overview

LSProcess is the primary abstraction for executable workflows in the LSProcessSystem. It serves as both a data container and execution orchestrator, supporting complex business logic through configurable processing trees.

**Core Architecture:**

- **Process** = Data container with execution lifecycle
- **LSProcessSession** = Runtime execution state management  
- **Node Hierarchy** = Actual processing logic and flow control

### LSProcess API

```csharp
public abstract class LSProcess
{
    // Identity & Lifecycle
    public Guid ID { get; }                    // Auto-generated unique identifier
    public DateTime CreatedAt { get; }         // UTC creation timestamp
    public bool IsCompleted { get; }           // True if SUCCESS/FAILURE/CANCELLED
    public bool IsCancelled { get; }          // True if execution was cancelled
    
    // Execution Methods
    public LSProcessResultStatus Execute(params ILSProcessable[]? instances)
    public LSProcessResultStatus Execute(LSProcessManager manager, ProcessInstanceBehaviour instanceBehaviour, params ILSProcessable[]? instances)
    
    // Async Control Methods (post-execution)
    public LSProcessResultStatus Resume(params string[] nodeIDs)   // Resume WAITING nodes
    public LSProcessResultStatus Fail(params string[] nodeIDs)     // Force WAITING→FAILURE
    public void Cancel()                                           // Force→CANCELLED (terminal)
    
    // Configuration & Data
    public LSProcess WithProcessing(LSProcessBuilderAction builderAction, LSProcessLayerNodeType rootType = LSProcessLayerNodeType.SELECTOR)
    protected virtual LSProcessTreeBuilder processing(LSProcessTreeBuilder builder)  // Override to extend/modify processing logic
    
    // Data Exchange (for inter-handler communication)
    public void SetData<T>(string key, T value)                   // Store typed data
    public T? GetData<T>(string key)                              // Retrieve typed data (throws if missing/wrong type)
    public bool TryGetData<T>(string key, out T value)            // Safe retrieval
}
```

### Key Features & Usage Patterns

#### 1. Single Execution Model

```csharp
var process = new MyProcess();
var result = process.Execute(); // First call - executes the workflow
var result2 = process.Execute(); // Subsequent calls - returns cached status
```

#### 2. Data Container Pattern

```csharp
// Handlers communicate through the process data store
LSProcessHandler storeDataHandler = session => {
    session.Process.SetData("userId", 12345);
    session.Process.SetData("timestamp", DateTime.UtcNow);
    return LSProcessResultStatus.SUCCESS;
};

LSProcessHandler retrieveDataHandler = session => {
    var userId = session.Process.GetData<int>("userId");
    var timestamp = session.Process.GetData<DateTime>("timestamp");
    
    // Safe retrieval alternative
    if (session.Process.TryGetData<string>("optionalData", out var data)) {
        // Use optional data
    }
    return LSProcessResultStatus.SUCCESS;
};
```

#### 3. Processing Configuration - Combined Approach

```csharp
// Approach A: Runtime configuration with WithProcessing()
var process = new MyProcess()
    .WithProcessing(builder => builder
        .Sequence("validation", seq => seq
            .Handler("checkInput", inputValidator)
            .Handler("validateBusiness", businessValidator))
        .Handler("process", mainProcessor)
        .Handler("cleanup", cleanupHandler));

// Approach B: Override processing() to extend/modify WithProcessing() result
public class ValidationProcess : LSProcess 
{
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) 
    {
        // Builder already contains WithProcessing() configuration
        // Add additional built-in handlers
        return builder
            .Handler("built-in-validation", CheckFormat)
            .Handler("built-in-security", CheckSecurity);
    }
    
    private LSProcessResultStatus CheckFormat(LSProcessSession session) => LSProcessResultStatus.SUCCESS;
    private LSProcessResultStatus CheckSecurity(LSProcessSession session) => LSProcessResultStatus.SUCCESS;
}

// Combined usage: WithProcessing() + processing() override work together
var combinedProcess = new ValidationProcess()
    .WithProcessing(builder => builder.Handler("runtime-config", runtimeHandler));
// Result: Contains both runtime handlers AND built-in handlers
var result = combinedProcess.Execute();
```

#### 4. Asynchronous Operations & Control

```csharp
// Handler that requires external completion
LSProcessHandler asyncHandler = session => {
    StartAsyncOperation(); // Start external operation
    return LSProcessResultStatus.WAITING; // Process will wait
};

var process = new MyProcess()
    .WithProcessing(b => b.Handler("async", asyncHandler));

var result = process.Execute();        // Returns WAITING
// ... external event occurs ...
var finalResult = process.Resume("async");  // Continue execution

// Alternative: Force failure
var failResult = process.Fail("async");     // Force WAITING→FAILURE

// Terminal cancellation
process.Cancel();                           // Force→CANCELLED (cannot resume)
```

#### 5. Context Merging System

```csharp
// Priority order: Local > Instance-specific > Global

// 1. Global context (lowest priority)
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("global", globalHandler));

// 2. Instance-specific context  
var entity = new GameEntity();
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("entity-specific", entityHandler), entity);

// 3. Local context (highest priority)
var process = new MyProcess()
    .WithProcessing(builder => builder
        .Handler("local", localHandler)); // This executes first

var result = process.Execute(entity); // Merges all three contexts
```

### Advanced Usage Patterns

#### Complex Workflow Design

```csharp
public class OrderProcessingWorkflow : LSProcess 
{
    public Order Order { get; set; }
    
    public OrderProcessingWorkflow ConfigureFor(Order order) 
    {
        Order = order;
        
        return this.WithProcessing(builder => builder
            .Sequence("order-processing", main => main
                .Handler("validate-order", ValidateOrder)
                .Selector("payment-strategy", payment => payment
                    .Sequence("credit-card", cc => cc
                        .Handler("validate-card", ValidateCreditCard)
                        .Handler("charge-card", ChargeCreditCard))
                    .Handler("paypal", ProcessPayPal)
                    .Handler("bank-transfer", ProcessBankTransfer))
                .Parallel("fulfillment", fulfill => fulfill
                    .Handler("reserve-inventory", ReserveInventory)
                    .Handler("calculate-shipping", CalculateShipping)
                    .Handler("generate-invoice", GenerateInvoice), 
                    numRequiredToSucceed: 3) // All must succeed
                .Handler("finalize-order", FinalizeOrder)));
    }
    
    private LSProcessResultStatus ValidateOrder(LSProcessSession session) 
    {
        if (Order == null || Order.Items.Count == 0) 
        {
            session.Process.SetData("error", "Invalid order");
            return LSProcessResultStatus.FAILURE;
        }
        
        session.Process.SetData("validated-order", Order);
        return LSProcessResultStatus.SUCCESS;
    }
    
    // ... other handler implementations
}

// Usage
var workflow = new OrderProcessingWorkflow().ConfigureFor(customerOrder);
var result = workflow.Execute();

if (result == LSProcessResultStatus.WAITING) 
{
    // Handle async operations (payment processing, inventory checks, etc.)
    // Resume when external systems respond
    await WaitForExternalSystems();
    result = workflow.Resume(); // Resume all waiting nodes
}
```

### Best Practices for LSProcess

1. **Inheritance Patterns**: Flexible combination of runtime and built-in logic

```csharp
// Approach 1: Data-only inheritance with WithProcessing()
public class UserRegistrationProcess : LSProcess 
{
    public string Email { get; set; }
    public string Username { get; set; }
}

// Usage: Pure runtime configuration
var process = new UserRegistrationProcess()
    .WithProcessing(builder => builder.Handler("validate", handler));

// Approach 2: Override processing() to extend WithProcessing()
public class OrderProcess : LSProcess 
{
    public Order Order { get; set; }
    
    protected override LSProcessTreeBuilder processing(LSProcessTreeBuilder builder) 
    {
        // Builder already contains WithProcessing() configuration
        // Add built-in handlers that always execute
        return builder
            .Handler("built-in-validation", ValidateOrder)
            .Handler("built-in-audit", AuditOrder);
    }
    
    private LSProcessResultStatus ValidateOrder(LSProcessSession session) 
    {
        // Built-in validation always runs
        return LSProcessResultStatus.SUCCESS;
    }
    
    private LSProcessResultStatus AuditOrder(LSProcessSession session) 
    {
        // Built-in audit always runs
        return LSProcessResultStatus.SUCCESS;
    }
}

// Usage: Combined approach - runtime + built-in
var orderProcess = new OrderProcess { Order = myOrder }
    .WithProcessing(builder => builder
        .Handler("custom-validation", customHandler)
        .Handler("payment", paymentHandler));
// Result: Contains custom-validation, payment, built-in-validation, built-in-audit
var result = orderProcess.Execute();
```

2. **Data Exchange**: Use strongly-typed data for handler communication

```csharp
// Good: Type-safe data exchange
session.Process.SetData("user-id", 12345);
var userId = session.Process.GetData<int>("user-id");

// Better: Use constants for keys
public static class ProcessKeys 
{
    public const string UserId = "user-id";
    public const string ValidationResult = "validation-result";
}
```

3. **Error Handling**: Return appropriate status codes, avoid exceptions in handlers

```csharp
LSProcessHandler safeHandler = session => {
    try {
        // Business logic
        return LSProcessResultStatus.SUCCESS;
    } catch (BusinessException ex) {
        session.Process.SetData("error", ex.Message);
        return LSProcessResultStatus.FAILURE;
    } catch (Exception ex) {
        // Log critical errors but don't let them escape
        Logger.Error(ex);
        return LSProcessResultStatus.FAILURE;
    }
};
```

4. **Async Operations**: Use WAITING status appropriately

```csharp
LSProcessHandler asyncHandler = session => {
    var operationId = StartAsyncOperation();
    session.Process.SetData("operation-id", operationId);
    return LSProcessResultStatus.WAITING; // Will require Resume() later
};
```

## Supporting Components

### LSProcessManager

**Singleton for global context registration**

```csharp
// Register global contexts
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("global", globalHandler));

// Register instance-specific contexts  
LSProcessManager.Singleton.Register<MyProcess>(root => root
    .Handler("entity-specific", entityHandler), specificEntity);
```

### LSProcessTreeBuilder  

**Fluent API for building node hierarchies**

```csharp
// Main node types
builder.Sequence("nodeId", subBuilder => { })      // Execute children sequentially (AND logic)
builder.Selector("nodeId", subBuilder => { })      // Execute until first success (OR logic)  
builder.Parallel("nodeId", subBuilder => { }, 2)   // Execute concurrently with thresholds
builder.Handler("nodeId", handlerDelegate)         // Terminal execution node
```

### LSProcessHandler

**Delegate signature for business logic**

```csharp
public delegate LSProcessResultStatus LSProcessHandler(LSProcessSession session);

// Access process data in handlers
LSProcessHandler handler = session => {
    var input = session.Process.GetData<string>("key");
    session.Process.SetData("result", processedValue);
    return LSProcessResultStatus.SUCCESS;
};
```

## Node Types Reference

**Sequence** - Execute children sequentially (AND logic)

- ✅ All children must succeed → SUCCESS
- ❌ First failure/cancellation → FAILURE/CANCELLED
- 🔄 Use for: Validation chains, step-by-step workflows

**Selector** - Execute until first success (OR logic)  

- ✅ Any child succeeds → SUCCESS
- ❌ All children fail → FAILURE
- 🔄 Use for: Fallback strategies, alternative paths

**Parallel** - Execute children concurrently with thresholds

- ⚙️ Configurable success/failure count requirements
- 🔄 Use for: Concurrent operations, majority voting

**Handler** - Terminal nodes that execute your business logic

- 🎯 Contains LSProcessHandler delegate
- 🔄 Use for: Actual work implementation

## Status Codes

### LSProcessResultStatus

```csharp
public enum LSProcessResultStatus
{
    UNKNOWN,    // Initial state, not yet processed
    SUCCESS,    // Completed successfully (terminal)
    FAILURE,    // Failed execution (terminal)  
    WAITING,    // Waiting for external event (resumable)
    CANCELLED   // Operation cancelled (terminal)
}
```

**Status Priority in Aggregation:**
CANCELLED > WAITING > FAILURE > SUCCESS > UNKNOWN

## Quick Start Examples

### 1. Basic Process Creation & Execution

```csharp
public class UserRegistration : LSProcess 
{
    public string Email { get; set; }
    public string Username { get; set; }
}

var process = new UserRegistration { Email = "user@example.com" };
var result = process.Execute(); // Uses registered global context
```

### 2. Local Processing Logic  

```csharp
var process = new UserRegistration()
    .WithProcessing(builder => builder
        .Sequence("registration", seq => seq
            .Handler("validate", session => {
                var email = session.Process.GetData<string>("email");
                return string.IsNullOrEmpty(email) ? 
                    LSProcessResultStatus.FAILURE : 
                    LSProcessResultStatus.SUCCESS;
            })
            .Handler("create-user", session => {
                // Create user logic
                return LSProcessResultStatus.SUCCESS;
            })));

var result = process.Execute();
```

### 3. Async Operations with Resume/Fail

```csharp
LSProcessHandler emailVerification = session => {
    var email = session.Process.GetData<string>("email");
    SendVerificationEmail(email);
    return LSProcessResultStatus.WAITING; // Wait for user click
};

var process = new UserRegistration()
    .WithProcessing(b => b.Handler("verify-email", emailVerification));

var result = process.Execute();        // Returns WAITING
// ... user clicks verification link ...
result = process.Resume("verify-email"); // Continue execution

// Or handle timeout
result = process.Fail("verify-email");   // Force failure
```

### 4. Data Exchange Between Handlers

```csharp
var process = new OrderProcess()
    .WithProcessing(builder => builder
        .Sequence("order-flow", seq => seq
            .Handler("validate", session => {
                session.Process.SetData("order-total", 99.99m);
                session.Process.SetData("customer-id", 12345);
                return LSProcessResultStatus.SUCCESS;
            })
            .Handler("process-payment", session => {
                var total = session.Process.GetData<decimal>("order-total");
                var customerId = session.Process.GetData<int>("customer-id");
                // Process payment using stored data
                return LSProcessResultStatus.SUCCESS;
            })));
```

### 5. Error Handling with Fallbacks

```csharp
var process = new PaymentProcess()
    .WithProcessing(builder => builder
        .Selector("payment-options", sel => sel
            .Handler("credit-card", session => {
                try {
                    ProcessCreditCard();
                    return LSProcessResultStatus.SUCCESS;
                } catch {
                    return LSProcessResultStatus.FAILURE; // Try next option
                }
            })
            .Handler("paypal", ProcessPayPal)
            .Handler("bank-transfer", ProcessBankTransfer)));
```

## LSProcess Best Practices

### Design Patterns

- **Inherit from LSProcess**: Create domain-specific classes with typed properties
- **Single Execution**: Remember Execute() works only once per instance  
- **Data as State**: Use SetData/GetData for handler communication, not static variables
- **Composition over Complexity**: Prefer multiple simple processes over one complex process

### Error Handling

- **Status Codes over Exceptions**: Return FAILURE instead of throwing in handlers
- **Graceful Degradation**: Use Selector nodes for fallback strategies
- **Timeout Handling**: Implement timeout logic for WAITING states using Fail()

### Async Operations  

- **WAITING for External Events**: Use when operation requires external completion
- **Resume Specific Nodes**: Target specific nodeIDs in Resume() for precise control
- **Monitor Process State**: Check IsCompleted/IsCancelled for long-running processes

### Performance & Debugging

- **Meaningful Node IDs**: Use descriptive names for easier debugging and Resume operations  
- **Context Strategy**: Local contexts for instance-specific, Global for shared behavior
- **Process Lifecycle**: Track CreatedAt and ID for monitoring and audit trails
