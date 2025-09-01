# LSEventSystem Logging Reference Guide

## Overview

The LSEventSystem includes a comprehensive, enterprise-grade logging infrastructure designed to provide flexible, structured, and performant logging capabilities. This system replaces the previous event data pollution approach with a clean, pluggable architecture that supports multiple output targets and configurable verbosity levels.

## Architecture Components

### Core Components

1. **LSEventLogger** - Central logging coordinator (singleton)
2. **ILSLogProvider** - Interface for pluggable output providers
3. **LSLogEntry** - Immutable structured log entry
4. **LSLogLevel** - Hierarchical severity levels
5. **Log Providers** - Console, Godot, and Null implementations

### Design Principles

- **No Business Data Pollution**: Logging is completely separate from event data
- **Thread-Safe Operations**: Concurrent access from multiple threads
- **Fail-Safe Design**: Logging failures never break application flow
- **Performance Optimized**: Level filtering prevents unnecessary processing
- **Extensible Architecture**: Easy to add new output providers

## Log Levels

The system uses a hierarchical 5-level severity system:

| Level | Value | Purpose | When to Use |
|-------|-------|---------|-------------|
| **DEBUG** | 0 | Detailed debugging information | Development, troubleshooting |
| **INFO** | 1 | General operational information | Normal system monitoring |
| **WARNING** | 2 | Potential issues, non-critical | Event cancellations, retries |
| **ERROR** | 3 | Serious issues requiring attention | Handler failures, business rule violations |
| **CRITICAL** | 4 | System-threatening problems | Exceptions, data corruption, security issues |

### Level Filtering

Each level includes messages from its level and above:

- `DEBUG` → Shows all 5 levels
- `INFO` → Shows INFO, WARNING, ERROR, CRITICAL
- `WARNING` → Shows WARNING, ERROR, CRITICAL
- `ERROR` → Shows ERROR, CRITICAL
- `CRITICAL` → Shows CRITICAL only

## Quick Start

### Basic Usage

```csharp
// Get the singleton instance
var logger = LSEventLogger.Instance;

// Simple logging
logger.Debug("Handler execution started");
logger.Info("Event processing completed successfully");
logger.Warning("Event cancelled due to validation failure");
logger.Error("Handler execution failed after retries");
logger.Critical("Unhandled exception in event processing");
```

### Setting Log Level

```csharp
// Set minimum level (filters out lower levels)
LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;

// Enable/disable logging entirely
LSEventLogger.Instance.IsEnabled = false;
```

### Provider Management

```csharp
// Clear existing providers
LSEventLogger.Instance.ClearProviders();

// Add specific providers
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());

// Disable all logging (production mode)
LSEventLogger.Instance.DisableLogging();
```

## Advanced Usage

### Structured Logging

```csharp
// Logging with structured properties
var properties = new Dictionary<string, object> {
    ["user_id"] = "12345",
    ["operation"] = "user_registration",
    ["duration_ms"] = 150.5,
    ["ip_address"] = "192.168.1.100"
};

LSEventLogger.Instance.Info(
    "User registration completed", 
    source: "UserService",
    eventId: Guid.NewGuid(),
    properties: properties
);
```

### Exception Logging

```csharp
try {
    // Risky operation
    ProcessUserData();
} catch (Exception ex) {
    // Log exception with context
    LSEventLogger.Instance.Critical(
        "Failed to process user data", 
        exception: ex,
        source: "DataProcessor",
        properties: new Dictionary<string, object> {
            ["user_count"] = userList.Count,
            ["batch_id"] = batchId
        }
    );
    
    // Or use the convenience method
    LSEventLogger.Instance.LogException(ex, "User data processing failed");
}
```

### Safe Execution Wrappers

```csharp
// Safe action execution (logs exceptions but doesn't re-throw)
LSEventLogger.Instance.SafeExecute(() => {
    // Potentially risky operation
    UpdateDatabase();
}, context: "Database update operation");

// Safe function execution with fallback value
var result = LSEventLogger.Instance.SafeExecute(() => {
    return CalculateComplexValue();
}, defaultValue: 0, context: "Complex calculation");
```

## Log Providers

### Console Provider (LSConsoleLogProvider)

**Features:**

- Color-coded output based on log level
- Standard console output
- Always available
- Best for development and desktop applications

**Colors:**

- DEBUG: Gray
- INFO: White  
- WARNING: Yellow
- ERROR: Red
- CRITICAL: Magenta

**Usage:**

```csharp
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
```

### Godot Provider (LSGodotLogProvider)

**Features:**

- Integrates with Godot's debug console
- Uses reflection to avoid hard dependencies
- Auto-detects Godot availability
- Routes WARNING/ERROR/CRITICAL to PrintErr

**Usage:**

```csharp
LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());

// Check if Godot is available
if (new LSGodotLogProvider().IsAvailable) {
    // Godot logging will work
}
```

### Null Provider (LSNullLogProvider)

**Features:**

- Discards all log entries
- Zero performance overhead
- Perfect for production deployments
- Always available

**Usage:**

```csharp
// Production mode - disable all logging
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new LSNullLogProvider());

// Or use the convenience method
LSEventLogger.Instance.DisableLogging();
```

## Integration with LSEventSystem

### Automatic Dispatcher Logging

The LSDispatcher automatically logs events at appropriate levels:

```csharp
// These are logged automatically by the dispatcher
var user = new User { Email = "test@example.com" };
var userEvent = new UserRegistrationEvent(user, "web");

userEvent.WithCallbacks<UserRegistrationEvent>(dispatcher)
    .OnValidatePhase((evt, ctx) => {
        // DEBUG: Handler execution details
        // WARNING: If handler returns CANCEL
        // ERROR: If handler has persistent failures
        return LSHandlerResult.CONTINUE;
    })
    .OnExecutePhase((evt, ctx) => {
        // Exceptions automatically logged as CRITICAL
        if (someCondition) {
            throw new InvalidOperationException("Business rule violation");
        }
        return LSHandlerResult.CONTINUE;
    })
    .Dispatch();
```

### Custom Business Logic Logging

```csharp
userEvent.WithCallbacks<UserRegistrationEvent>(dispatcher)
    .OnValidatePhase((evt, ctx) => {
        // Custom business logging
        LSEventLogger.Instance.Info($"Validating user registration for {evt.Instance.Email}");
        
        if (string.IsNullOrEmpty(evt.Instance.Email)) {
            LSEventLogger.Instance.Warning("Email validation failed - empty email");
            return LSHandlerResult.CANCEL;
        }
        
        LSEventLogger.Instance.Debug("Email validation passed");
        return LSHandlerResult.CONTINUE;
    })
    .OnExecutePhase((evt, ctx) => {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try {
            CreateUserAccount(evt.Instance);
            
            LSEventLogger.Instance.Info("User account created successfully", 
                properties: new Dictionary<string, object> {
                    ["duration_ms"] = sw.ElapsedMilliseconds,
                    ["user_email"] = evt.Instance.Email
                });
                
        } catch (Exception ex) {
            LSEventLogger.Instance.Error("Failed to create user account", ex);
            throw; // Re-throw to maintain event system behavior
        }
        
        return LSHandlerResult.CONTINUE;
    })
    .Dispatch();
```

## Custom Log Providers

### Creating Custom Providers

```csharp
public class FileLogProvider : ILSLogProvider {
    private readonly string _filePath;
    
    public FileLogProvider(string filePath) {
        _filePath = filePath;
    }
    
    public string Name => "File";
    
    public bool IsAvailable => !string.IsNullOrEmpty(_filePath);
    
    public void WriteLog(LSLogEntry entry) {
        try {
            var logLine = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Level}] {entry.Message}";
            File.AppendAllText(_filePath, logLine + Environment.NewLine);
        } catch {
            // Silent failure - logging should never break the application
        }
    }
}

// Usage
LSEventLogger.Instance.AddProvider(new FileLogProvider("application.log"));
```

### Database Log Provider

```csharp
public class DatabaseLogProvider : ILSLogProvider {
    private readonly string _connectionString;
    
    public DatabaseLogProvider(string connectionString) {
        _connectionString = connectionString;
    }
    
    public string Name => "Database";
    
    public bool IsAvailable => !string.IsNullOrEmpty(_connectionString);
    
    public void WriteLog(LSLogEntry entry) {
        try {
            // Insert log entry into database
            using var connection = new SqlConnection(_connectionString);
            var command = new SqlCommand(
                "INSERT INTO Logs (Timestamp, Level, Message, Source, Exception, EventId) " +
                "VALUES (@timestamp, @level, @message, @source, @exception, @eventId)", 
                connection);
                
            command.Parameters.AddWithValue("@timestamp", entry.Timestamp);
            command.Parameters.AddWithValue("@level", entry.Level.ToString());
            command.Parameters.AddWithValue("@message", entry.Message);
            command.Parameters.AddWithValue("@source", entry.Source);
            command.Parameters.AddWithValue("@exception", entry.Exception?.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("@eventId", entry.EventId ?? (object)DBNull.Value);
            
            connection.Open();
            command.ExecuteNonQuery();
        } catch {
            // Silent failure - logging should never break the application
        }
    }
}
```

## Configuration Examples

### Development Configuration

```csharp
// Development: Show everything, use console with colors
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
LSEventLogger.Instance.IsEnabled = true;
```

### Testing Configuration

```csharp
// Testing: Important messages only, use console
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;
LSEventLogger.Instance.IsEnabled = true;
```

### Production Configuration

```csharp
// Production: Errors only, use file and database
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new FileLogProvider("logs/application.log"));
LSEventLogger.Instance.AddProvider(new DatabaseLogProvider(connectionString));
LSEventLogger.Instance.MinimumLevel = LSLogLevel.ERROR;
LSEventLogger.Instance.IsEnabled = true;
```

### Godot Game Configuration

```csharp
// Godot: Auto-detect Godot, fallback to console
LSEventLogger.Instance.ClearProviders();
if (new LSGodotLogProvider().IsAvailable) {
    LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());
} else {
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
}
LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;
```

### Silent Mode (Performance Critical)

```csharp
// High-performance mode: No logging overhead
LSEventLogger.Instance.DisableLogging();
// or
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new LSNullLogProvider());
```

## Output Format Examples

### Console Output

```text
2025-09-01 18:14:58.757 [DEBUG   ] LSDispatcher        : ProcessEventInternal: Starting processing (resuming: False) [Event: c39fd4b2e4564370b669c5841b7f1aa7]
  Properties:
    phase: VALIDATE
    cancelled: False
    waiting: False
    completed: False
    hasFailures: False

2025-09-01 18:14:58.790 [INFO    ] LSEventSystem       : INFO: Processing for DEBUG level test

2025-09-01 18:14:58.790 [WARNING ] LSEventSystem       : WARNING: Potential issue for DEBUG level test

2025-09-01 18:14:58.791 [ERROR   ] LSEventSystem       : ERROR: Simulated error for DEBUG level test

2025-09-01 18:14:58.041 [CRITICAL] LSEventSystem       : Handler d52e1501-015e-4d1e-bde9-723ab6a06308 threw exception in phase EXECUTE
  Exception: InvalidOperationException: Demo exception for CRITICAL logging
  Stack Trace:    at LSUtils.Tests.LoggingDemonstration.<>c.<Demo_Critical_Exception_Handling>b__7_0(DemoUserRegistrationEvent evt, LSPhaseContext ctx) in D:\Projetos\LSUtils\src\LSEventSystem\Tests\LoggingDemonstration.cs:line 185
    at LSUtils.EventSystem.LSDispatcher.ExecuteBatchedHandlers[TEvent](TEvent evt, LSPhaseContext ctx, LSEventCallbackBatch`1 batch) in D:\Projetos\LSUtils\src\LSEventSystem\LSDispatcher.cs:line 769
```

## Performance Considerations

### Level Filtering Performance

```csharp
// This is efficient - message not processed if below minimum level
LSEventLogger.Instance.MinimumLevel = LSLogLevel.WARNING;
LSEventLogger.Instance.Debug("Expensive string interpolation: " + ExpensiveOperation());
// ExpensiveOperation() is NOT called when level is WARNING

// For expensive operations, check level first
if (LSEventLogger.Instance.MinimumLevel <= LSLogLevel.DEBUG) {
    var expensiveData = PerformExpensiveCalculation();
    LSEventLogger.Instance.Debug($"Calculation result: {expensiveData}");
}
```

### Provider Performance

```csharp
// Multiple providers: all receive the log entry
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());     // Fast
LSEventLogger.Instance.AddProvider(new FileLogProvider("log.txt")); // Medium
LSEventLogger.Instance.AddProvider(new DatabaseLogProvider(conn));  // Slower

// Consider async providers for high-volume scenarios
public class AsyncFileLogProvider : ILSLogProvider {
    private readonly BlockingCollection<LSLogEntry> _queue = new();
    
    public AsyncFileLogProvider(string filePath) {
        // Background thread processes queue
        Task.Run(() => ProcessQueue(filePath));
    }
    
    public void WriteLog(LSLogEntry entry) {
        _queue.TryAdd(entry); // Non-blocking
    }
    
    private void ProcessQueue(string filePath) {
        foreach (var entry in _queue.GetConsumingEnumerable()) {
            File.AppendAllText(filePath, entry.ToString() + Environment.NewLine);
        }
    }
}
```

## Best Practices

### 1. Use Appropriate Log Levels

```csharp
// ✅ Good: Appropriate levels
LSEventLogger.Instance.Debug("Handler parameter validation");
LSEventLogger.Instance.Info("User registration completed");
LSEventLogger.Instance.Warning("Event cancelled - invalid email");
LSEventLogger.Instance.Error("Database connection failed");
LSEventLogger.Instance.Critical("Unhandled exception in critical path");

// ❌ Bad: Wrong levels
LSEventLogger.Instance.Critical("Debug information");  // Don't do this
LSEventLogger.Instance.Debug("Critical system failure"); // Don't do this
```

### 2. Include Contextual Information

```csharp
// ✅ Good: Rich context
LSEventLogger.Instance.Error("User validation failed", 
    properties: new Dictionary<string, object> {
        ["user_id"] = user.Id,
        ["validation_rule"] = "email_format",
        ["attempted_email"] = user.Email,
        ["request_id"] = requestId
    });

// ❌ Bad: Minimal context
LSEventLogger.Instance.Error("Validation failed");
```

### 3. Handle Exceptions Properly

```csharp
// ✅ Good: Log and decide whether to re-throw
try {
    ProcessData();
} catch (ValidationException ex) {
    LSEventLogger.Instance.Warning("Validation failed", ex);
    return false; // Handle gracefully
} catch (Exception ex) {
    LSEventLogger.Instance.Critical("Unexpected error in data processing", ex);
    throw; // Re-throw unexpected exceptions
}

// ❌ Bad: Swallow all exceptions
try {
    ProcessData();
} catch (Exception ex) {
    LSEventLogger.Instance.Error("Something went wrong", ex);
    // Silently continuing can hide serious issues
}
```

### 4. Use Structured Properties

```csharp
// ✅ Good: Structured data
var properties = new Dictionary<string, object> {
    ["operation"] = "user_creation",
    ["user_email"] = user.Email,
    ["source_ip"] = request.IPAddress,
    ["duration_ms"] = stopwatch.ElapsedMilliseconds
};
LSEventLogger.Instance.Info("Operation completed", properties: properties);

// ❌ Bad: String concatenation
LSEventLogger.Instance.Info($"User {user.Email} created from {request.IPAddress} in {stopwatch.ElapsedMilliseconds}ms");
```

### 5. Configure Appropriately for Environment

```csharp
// ✅ Good: Environment-specific configuration
#if DEBUG
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
#else
    LSEventLogger.Instance.MinimumLevel = LSLogLevel.WARNING;
    LSEventLogger.Instance.AddProvider(new FileLogProvider("logs/production.log"));
#endif
```

## Troubleshooting

### Common Issues

1. **No Log Output**

   ```csharp
   // Check if logging is enabled
   if (!LSEventLogger.Instance.IsEnabled) {
       LSEventLogger.Instance.IsEnabled = true;
   }
   
   // Check if any providers are registered
   LSEventLogger.Instance.ClearProviders();
   LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
   
   // Check minimum level
   LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
   ```

2. **Godot Provider Not Working**

   ```csharp
   // Check availability
   var godotProvider = new LSGodotLogProvider();
   if (!godotProvider.IsAvailable) {
       // Godot not detected, use console instead
       LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
   }
   ```

3. **Performance Issues**

   ```csharp
   // Increase minimum level to reduce processing
   LSEventLogger.Instance.MinimumLevel = LSLogLevel.WARNING;
   
   // Remove expensive providers
   LSEventLogger.Instance.ClearProviders();
   LSEventLogger.Instance.AddProvider(new LSNullLogProvider()); // For production
   ```

### Debugging Logging Issues

```csharp
// Create a test method to verify logging setup
public void TestLoggingSetup() {
    var logger = LSEventLogger.Instance;
    
    Console.WriteLine($"Logging enabled: {logger.IsEnabled}");
    Console.WriteLine($"Minimum level: {logger.MinimumLevel}");
    
    // Test each level
    logger.Debug("Debug test message");
    logger.Info("Info test message");
    logger.Warning("Warning test message");
    logger.Error("Error test message");
    logger.Critical("Critical test message");
    
    // Test with exception
    try {
        throw new InvalidOperationException("Test exception");
    } catch (Exception ex) {
        logger.Critical("Exception test", ex);
    }
}
```

## Migration Guide

### From Old Event Data Logging

```csharp
// ❌ Old way: Polluting event data
@event.SetData("log.level", "INFO");
@event.SetData("log.message", "Handler executed");
@event.SetData("log.timestamp", DateTime.UtcNow);

// ✅ New way: Clean logging
LSEventLogger.Instance.Info("Handler executed", 
    source: "EventHandler", 
    eventId: @event.ID);
```

### From Console.WriteLine

```csharp
// ❌ Old way: Direct console output
Console.WriteLine($"[{DateTime.Now}] INFO: Processing event {eventId}");

// ✅ New way: Structured logging
LSEventLogger.Instance.Info("Processing event", 
    properties: new Dictionary<string, object> { ["event_id"] = eventId });
```

This reference guide provides comprehensive coverage of the LSEventSystem logging infrastructure, enabling developers to leverage its full capabilities for robust, maintainable, and observable applications.
