# LSEventSystem Logging - Quick Reference

## 🚀 Quick Start

```csharp
// Get logger instance
var logger = LSEventLogger.Instance;

// Basic logging
logger.Debug("Detailed debug info");
logger.Info("General information");
logger.Warning("Potential issue");
logger.Error("Serious problem");
logger.Critical("Critical failure");
```

## ⚙️ Configuration

```csharp
// Set log level (filters out lower levels)
LSEventLogger.Instance.MinimumLevel = LSLogLevel.INFO;

// Manage providers
LSEventLogger.Instance.ClearProviders();
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());

// Production mode (silent)
LSEventLogger.Instance.DisableLogging();
```

## 📊 Log Levels

| Level | When to Use | Example |
|-------|-------------|---------|
| **DEBUG** | Development, troubleshooting | Handler execution details |
| **INFO** | Normal operations | Event completed successfully |
| **WARNING** | Non-critical issues | Event cancelled |
| **ERROR** | Serious problems | Handler failed after retries |
| **CRITICAL** | System threats | Unhandled exceptions |

## 🔧 Advanced Usage

### Structured Logging

```csharp
logger.Info("User registered", 
    source: "UserService",
    eventId: user.EventId,
    properties: new Dictionary<string, object> {
        ["user_id"] = user.Id,
        ["email"] = user.Email,
        ["source"] = "web"
    });
```

### Exception Logging

```csharp
try {
    ProcessData();
} catch (Exception ex) {
    logger.Critical("Data processing failed", ex);
    throw;
}
```

### Safe Execution

```csharp
// Logs exceptions but doesn't re-throw
logger.SafeExecute(() => RiskyOperation());

// With fallback value
var result = logger.SafeExecute(() => Calculate(), defaultValue: 0);
```

## 🎯 Providers

### Console (Development)

```csharp
new LSConsoleLogProvider()  // Color-coded console output
```

### Godot (Game Development)

```csharp
new LSGodotLogProvider()    // Integrates with Godot debug console
```

### Null (Production)

```csharp
new LSNullLogProvider()     // Silent mode, zero overhead
```

## 🏃‍♂️ Environment Examples

### Development

```csharp
LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
```

### Production

```csharp
LSEventLogger.Instance.MinimumLevel = LSLogLevel.ERROR;
LSEventLogger.Instance.AddProvider(new FileLogProvider("app.log"));
```

### Godot Games

```csharp
if (new LSGodotLogProvider().IsAvailable) {
    LSEventLogger.Instance.AddProvider(new LSGodotLogProvider());
} else {
    LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
}
```

## ✅ Best Practices

1. **Use appropriate levels** - Don't log debug info as critical
2. **Include context** - Add relevant properties and event IDs  
3. **Handle exceptions properly** - Log then decide whether to re-throw
4. **Structure your data** - Use properties instead of string concatenation
5. **Configure per environment** - Debug level for dev, errors for production

## 🐛 Troubleshooting

**No output?**

```csharp
LSEventLogger.Instance.IsEnabled = true;
LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
```

**Too much output?**

```csharp
LSEventLogger.Instance.MinimumLevel = LSLogLevel.WARNING;
```

**Performance issues?**

```csharp
LSEventLogger.Instance.DisableLogging(); // Production mode
```

---
📖 **Full Documentation:** See `README.md` for comprehensive guide and examples
