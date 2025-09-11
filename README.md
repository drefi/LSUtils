# LSUtils - Utility Library for .NET 8

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NUnit](https://img.shields.io/badge/NUnit-4.2.2-brightgreen)](https://nunit.org/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A comprehensive utility library providing advanced event systems, mathematical utilities, collections, and graph algorithms for .NET 8 applications.

## 🚀 Quick Start

```csharp
// Initialize the event system
var options = new LSEventOptions();
var dispatcher = options.Dispatcher;

// Register global phase handlers
dispatcher.ForEventPhase<UserRegistrationEvent, LSEventBusinessState.ValidatePhaseState>(
    register => register.Handler(ctx => {
        var user = ctx.Event.UserData;
        return user.IsValid ? HandlerProcessResult.SUCCESS : HandlerProcessResult.FAILURE;
    })
);

// Process events with comprehensive phase handling
var userEvent = new UserRegistrationEvent(options, newUser);
var result = userEvent.Dispatch();

switch (result) {
    case EventProcessResult.SUCCESS:
        Console.WriteLine("User registered successfully");
        break;
    case EventProcessResult.CANCELLED:
        Console.WriteLine("Registration was cancelled");
        break;
    case EventProcessResult.FAILURE:
        Console.WriteLine("Registration failed validation");
        break;
}
```

## 📋 Table of Contents

- [Features](#-features)
- [Components](#-components)
- [Installation](#-installation)
- [Getting Started](#-getting-started)
- [Event System Usage](#-event-system-usage)
- [Phase Cancellation](#-phase-cancellation)
- [Handler Registration](#-handler-registration)
- [State Management](#-state-management)
- [Examples](#-examples)
- [API Reference](#-api-reference)
- [Testing](#-testing)

## ✨ Features

### 🎯 LSEventSystem - Advanced Event Processing

- **Sequential Phase Processing**: Validate → Configure → Execute → Cleanup phases
- **Smart Cancellation Handling**: CleanupPhase cancellation preserves business success
- **Dual Registration Patterns**: Global dispatcher and event-scoped handler support
- **Event-Driven Processing**: Events initiate their own processing (dispatcher only registers handlers)
- **Type-safe Fluent API** with comprehensive compile-time checking
- **Asynchronous Operation Support** with external control and resumption
- **Comprehensive Error Handling** with detailed failure tracking
- **Priority-based Execution** within phases for precise control
- **Thread-safe Operations** with concurrent data structures

### 🧮 Mathematical Utilities

- **LSMath**: Extended mathematical functions and utilities
- **ILSVector2/ILSVector2I**: 2D vector implementations with mathematical operations
- **Random**: Enhanced random number generation utilities

### 📦 Collections & Data Structures

- **BinaryHeap**: Efficient priority queue implementation
- **CachePool**: Object pooling with cache management
- **Grid & Hex Graphs**: Spatial data structures for pathfinding

### 🗺️ Graph Algorithms

- **A* Pathfinding**: Optimized A* implementation for grid graphs
- **Dijkstra Algorithm**: Shortest path algorithm implementation
- **Hex Grid Support**: Hexagonal grid pathfinding and navigation

### 🎮 Game Development Utilities

- **LSTick**: Centralized tick/frame management system
- **LSState**: Generic state management with context support
- **LSSignals**: Event-based notification system

## 🏗️ Components

### Core Event System

```text
LSEventSystem/
├── LSDispatcher.cs           # Central event dispatcher
├── LSEvent.cs                # Base event classes
├── LSEventOptions.cs         # Configuration for events
├── LSEventProcessContext.cs  # Execution context
├── EventStates/              # State machine implementations
├── Enums/                    # Result and priority enumerations
├── Interfaces/               # Core interfaces
└── Logging/                  # Comprehensive logging system
```

### Utilities & Extensions

```text
src/
├── LSMath.cs                 # Mathematical utilities
├── LSSignals.cs              # Notification system
├── LSState.cs                # State management
├── LSTick.cs                 # Tick management
├── LSTimestamp.cs            # Time utilities
├── Collections/              # Data structures
├── Graphs/                   # Pathfinding algorithms
└── Hex/                      # Hexagonal grid support
```

## 📦 Installation

### From Source

```bash
git clone https://github.com/yourusername/LSUtils.git
cd LSUtils
dotnet build
```

### As Package Reference

```xml
<PackageReference Include="LSUtils" Version="*" />
```

## 🚀 Getting Started

### Basic Event System Usage

```csharp
using LSUtils.EventSystem;

// Create a custom event
public class UserActionEvent : LSEvent {
    public string Action { get; }
    public int UserId { get; }
    
    public UserActionEvent(LSEventOptions options, string action, int userId) 
        : base(options) {
        Action = action;
        UserId = userId;
        SetData("action", action);
        SetData("userId", userId);
    }
}

// Register and process events
var options = new LSEventOptions();
var userAction = new UserActionEvent(options, "login", 12345);
var result = userAction.Dispatch();
```

## 🎯 Event System Usage

### Quick Example

```csharp
// Define your event class
public class UserRegistrationEvent : LSEvent {
    public UserData UserData { get; }
    
    public UserRegistrationEvent(LSEventOptions options, UserData userData) 
        : base(options) {
        UserData = userData;
        SetData("userData", userData);
        SetData("registrationTime", DateTime.UtcNow);
    }
}

// Register global handlers
var options = new LSEventOptions();
var dispatcher = options.Dispatcher;

dispatcher.ForEventPhase<UserRegistrationEvent, LSEventBusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.HIGH)
        .Handler(ctx => {
            var userData = ctx.Event.UserData;
            return userData.IsValid() ? HandlerProcessResult.SUCCESS : HandlerProcessResult.FAILURE;
        })
);

// Process the event
var userEvent = new UserRegistrationEvent(options, newUser);
var result = userEvent.Dispatch();
```

For comprehensive documentation, see [LSEventSystem README](src/LSEventSystem/README.md).

## 🚫 Phase Cancellation

### Key Behavior

**Standard Phases (Validate, Configure, Execute)**: Cancellation → `EventProcessResult.CANCELLED`

**CleanupPhase Special Case**: Cancellation → `EventProcessResult.SUCCESS` (preserves business success)

```csharp
// CleanupPhase cancellation preserves success since core phases completed
dispatcher.ForEventPhase<MyEvent, LSEventBusinessState.CleanupPhaseState>(
    register => register.Handler(ctx => {
        try {
            CleanupResources();
            return HandlerProcessResult.SUCCESS;
        } catch (Exception) {
            return HandlerProcessResult.CANCELLED; // → SucceedState (not CancelledState!)
        }
    })
);
```

For detailed phase cancellation documentation, see [LSEventSystem README](src/LSEventSystem/README.md).

## 🔧 Handler Registration

### Global vs Event-Scoped

```csharp
// Global handlers (for all events of this type)
dispatcher.ForEventPhase<UserEvent, LSEventBusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.CRITICAL)
        .Handler(ctx => { /* Global validation */ })
);

// Event-scoped handlers (for specific event instances)
var userEvent = new UserEvent(options, userData)
    .WithPhaseCallbacks<LSEventBusinessState.ExecutePhaseState>(register => register
        .Handler(ctx => { /* Specific execution logic */ })
    );
```

For comprehensive handler registration patterns, see [LSEventSystem README](src/LSEventSystem/README.md).

## 🏛️ State Management

### Basic State Integration

```csharp
public class MainMenuState : LSState<MainMenuState, GameContext> {
    protected override void OnInitialize(LSEventOptions options) {
        // Register state-specific event handlers
        options.Dispatcher.ForEventPhase<MenuActionEvent, LSEventBusinessState.ExecutePhaseState>(
            register => register.Handler(ctx => {
                HandleMenuAction(ctx.Event);
                return HandlerProcessResult.SUCCESS;
            })
        );
    }
}
```

For complete state management patterns, see [LSEventSystem README](src/LSEventSystem/README.md).

## 📚 Examples

### Simple Order Processing

```csharp
public class OrderEvent : LSEvent {
    public int OrderId { get; }
    public decimal Amount { get; }
    
    public OrderEvent(LSEventOptions options, int orderId, decimal amount) 
        : base(options) {
        OrderId = orderId;
        Amount = amount;
        SetData("order_id", orderId);
        SetData("amount", amount);
    }
}

// Register basic processing pipeline
var options = new LSEventOptions();
var dispatcher = options.Dispatcher;

dispatcher.ForEventPhase<OrderEvent, LSEventBusinessState.ValidatePhaseState>(
    register => register.Handler(ctx => {
        var amount = ctx.Event.GetData<decimal>("amount");
        return amount > 0 ? HandlerProcessResult.SUCCESS : HandlerProcessResult.FAILURE;
    })
);

dispatcher.ForEventPhase<OrderEvent, LSEventBusinessState.ExecutePhaseState>(
    register => register.Handler(ctx => {
        var orderId = ctx.Event.GetData<int>("order_id");
        ProcessPayment(orderId);
        return HandlerProcessResult.SUCCESS;
    })
);

// Usage
var order = new OrderEvent(options, 12345, 99.99m);
var result = order.Dispatch();
```

For comprehensive examples including e-commerce, file processing, and advanced patterns, see [LSEventSystem README](src/LSEventSystem/README.md).

### Graph Pathfinding Example

```csharp
using LSUtils.Graphs;

// Create a grid graph
var graph = new GridGraph(10, 10);

// Set obstacles
graph.SetWalkable(5, 5, false);
graph.SetWalkable(5, 6, false);

// Find path using A*
var pathfinder = new AStarPathResolver<GridNode>();
var path = pathfinder.FindPath(
    graph.GetNode(0, 0),    // Start
    graph.GetNode(9, 9),    // Goal
    graph
);

if (path != null) {
    foreach (var node in path) {
        Console.WriteLine($"Step: ({node.X}, {node.Y})");
    }
}
```

## 📖 API Reference

### Core Classes

#### LSEventOptions

```csharp
public class LSEventOptions {
    public LSDispatcher Dispatcher { get; init; }
    public object? OwnerInstance { get; protected set; }
    
    public LSEventOptions OnSuccess(Func<LSStateHandlerRegister<SucceedState>, LSStateHandlerRegister<SucceedState>> callback);
    public LSEventOptions OnCancel(Func<LSStateHandlerRegister<CancelledState>, LSStateHandlerRegister<CancelledState>> callback);
    public LSEventOptions OnComplete(Func<LSStateHandlerRegister<CompletedState>, LSStateHandlerRegister<CompletedState>> callback);
}
```

#### LSEvent

```csharp
public abstract class LSEvent : ILSEvent {
    public Guid ID { get; }
    public DateTime CreatedAt { get; }
    public bool IsCancelled { get; }
    public bool HasFailures { get; }
    public bool IsCompleted { get; }
    
    public EventProcessResult Dispatch();
    public void SetData<T>(string key, T value);
    public T GetData<T>(string key);
    public bool TryGetData<T>(string key, out T value);
}
```

#### LSDispatcher

```csharp
public class LSDispatcher {
    public static LSDispatcher Singleton { get; }
    
    // Register handlers for events (does not process events directly)
    public Guid[] ForEvent<TEvent>(Func<LSEventRegister<TEvent>, Guid[]> configureRegister) where TEvent : ILSEvent;
    public Guid ForEventPhase<TEvent, TPhase>(Func<LSPhaseHandlerRegister<TPhase>, LSPhaseHandlerRegister<TPhase>> configurePhaseHandler) where TEvent : ILSEvent where TPhase : BusinessState.PhaseState;
    public Guid ForEventState<TEvent, TState>(Func<LSStateHandlerRegister<TState>, LSStateHandlerRegister<TState>> configureStateHandler) where TEvent : ILSEvent where TState : IEventProcessState;
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

#### LSPriority

```csharp
public enum LSPriority {
    BACKGROUND = 0,
    LOW = 1,
    NORMAL = 2,
    HIGH = 3,
    CRITICAL = 4
}
```

### Interfaces

#### ILSEventable

```csharp
public interface ILSEventable {
    LSDispatcher? Dispatcher { get; }
    void Initialize(LSEventOptions options);
}
```

#### ILSEvent

```csharp
public interface ILSEvent {
    Guid ID { get; }
    DateTime CreatedAt { get; }
    bool IsCancelled { get; }
    bool HasFailures { get; }
    bool IsCompleted { get; }
    
    void SetData<T>(string key, T value);
    T GetData<T>(string key);
    bool TryGetData<T>(string key, out T value);
    EventProcessResult Dispatch();
}
```

## 🧪 Testing

The project includes comprehensive test suites using NUnit:

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=EventSystem

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Test Structure

- **LSEventSystemTests**: Core event system functionality
- **Integration Tests**: Cross-component integration testing
- **Performance Tests**: Benchmarking and optimization validation

## 🔧 Advanced Configuration

### Logging Configuration

```csharp
// Configure logging for development
var options = new LSEventOptions()
    .OnValidatePhase(register => register
        .WithPriority(LSPriority.BACKGROUND)
        .Handler(ctx => {
            LSLog.Info($"Validating event {ctx.Event.ID}");
            return HandlerProcessResult.SUCCESS;
        }));
```

### Custom Event Types

```csharp
public class CustomBusinessEvent : LSEvent {
    public string BusinessId { get; }
    
    public CustomBusinessEvent(LSEventOptions options, string businessId) 
        : base(options) {
        BusinessId = businessId;
        SetData("business_id", businessId);
        SetData("timestamp", DateTime.UtcNow);
    }
}
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# naming conventions
- Add comprehensive tests for new features
- Update documentation for API changes
- Ensure backward compatibility when possible

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with ❤️ for the .NET community
- Inspired by enterprise event-driven architectures
- Designed for game development and business applications

---

**LSUtils** - Making .NET development more productive, one utility at a time! 🚀
