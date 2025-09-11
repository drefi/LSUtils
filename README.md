# LSUtils - Utility Library for .NET 8

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NUnit](https://img.shields.io/badge/NUnit-4.2.2-brightgreen)](https://nunit.org/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A comprehensive utility library providing event systems, mathematical utilities, collections, and graph algorithms for .NET 8 applications.

## 🚀 Quick Start

```csharp
// Initialize the event system
var options = new LSEventOptions();
var tick = LSTick.Singleton;
tick.Initialize(options);

// Subscribe to tick events
options.Dispatcher.ForEvent<LSTick.OnTickEvent>(register => register
    .OnPhase<BusinessState.ExecutePhaseState>(phase => phase
        .Handler(ctx => {
            Console.WriteLine($"Tick: {ctx.Event.TickCount}");
            return HandlerProcessResult.SUCCESS;
        })
        .Build())
    .Register());
```

## 📋 Table of Contents

- [Features](#-features)
- [Components](#-components)
- [Installation](#-installation)
- [Getting Started](#-getting-started)
- [Core Systems](#-core-systems)
- [Recent Changes](#-recent-changes)
- [Migration Guide](#-migration-guide)
- [Examples](#-examples)
- [API Reference](#-api-reference)
- [Contributing](#-contributing)

## ✨ Features

### 🎯 LSEventSystem v4

- **State-machine based event processing** with sequential phases
- **Type-safe handler registration** with fluent API
- **Asynchronous operation support** with external control
- **Comprehensive error handling** and cancellation support
- **Priority-based execution** within phases
- **Thread-safe data management** with concurrent dictionaries

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
<PackageReference Include="LSUtils" Version="4.0.0" />
```

## 🚀 Getting Started

### 1. Basic Event System Usage

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
        SetData("user_id", userId);
    }
}

// Register handlers
var dispatcher = LSDispatcher.Singleton;
dispatcher.ForEventPhase<UserActionEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.HIGH)
        .Handler(ctx => {
            var userId = ctx.Event.GetData<int>("user_id");
            if (userId <= 0) {
                return HandlerProcessResult.FAILURE;
            }
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Dispatch event
var options = new LSEventOptions(dispatcher);
var userEvent = new UserActionEvent(options, "login", 123);
var result = userEvent.Dispatch();
```

### 2. State Management

```csharp
public class GameContext : ILSContext {
    public string PlayerName { get; set; }
    public int Level { get; set; }
}

public class MenuState : LSState<MenuState, GameContext> {
    public MenuState(GameContext context) : base(context) { }
    
    public override void Cleanup() {
        // Cleanup resources
    }
}

// Usage
var context = new GameContext { PlayerName = "Player1", Level = 1 };
var menuState = new MenuState(context);
var options = new LSEventOptions();
menuState.Initialize(options);
```

### 3. Tick System

```csharp
// Initialize tick system
var tick = LSTick.Singleton;
var options = new LSEventOptions();
tick.Initialize(options);

// Subscribe to tick events
options.Dispatcher.ForEvent<LSTick.OnTickEvent>(register => register
    .OnPhase<BusinessState.ExecutePhaseState>(phase => phase
        .Handler(ctx => {
            var tickCount = ctx.Event.TickCount;
            // Update game logic
            return HandlerProcessResult.SUCCESS;
        })
        .Build())
    .Register());

// In your game loop
tick.Update(deltaTime);
```

## 🏗️ Core Systems

### LSEventSystem v4

The heart of LSUtils is the event system providing:

#### Phase-Based Processing

- **Validate**: Input validation and security checks
- **Configure**: Resource allocation and setup
- **Execute**: Core business logic
- **Cleanup**: Resource cleanup and finalization

#### State Machine

- **BusinessState**: Main processing state
- **SucceedState**: Success handling
- **CancelledState**: Cancellation cleanup  
- **CompletedState**: Final cleanup
- **WaitingState**: Asynchronous operation pause

#### Handler Registration

```csharp
// Global handlers
dispatcher.ForEventPhase<MyEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.CRITICAL)
        .When((evt, entry) => evt.GetData<bool>("requiresValidation"))
        .Handler(ctx => {
            // Validation logic
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Event-scoped handlers
var event = new MyEvent(options)
    .WithPhaseCallbacks<BusinessState.ExecutePhaseState>(
        register => register
            .Handler(ctx => {
                // Event-specific logic
                return HandlerProcessResult.SUCCESS;
            })
            .Build()
    );
```

## 🔄 Recent Changes

### Version 4.0 - Major Architectural Update

#### Breaking Changes

- **LSEventOptions Introduction**: Events now require `LSEventOptions` instead of direct dispatcher injection
- **Handler Registration Refactoring**: Constructors no longer require dispatcher parameters
- **State Management Modernization**: `LSState` now implements `ILSEventable` interface
- **Signal System Update**: `LSSignals` migrated to v4 event architecture

#### New Features

- **ILSEventable Interface**: Standardized initialization pattern for eventable objects
- **LSBasicEvents**: Factory methods for common event patterns
- **Enhanced Testing**: Improved test infrastructure with better isolation
- **Logging Integration**: Comprehensive logging system with multiple providers

#### Improvements

- **Better Separation of Concerns**: Cleaner architecture with reduced coupling
- **Improved Type Safety**: Enhanced generic constraints and type checking
- **Performance Optimizations**: Reduced object allocation in hot paths
- **Documentation**: Comprehensive API documentation and examples

### Migration from v3 to v4

#### Constructor Changes

```csharp
// v3
var register = new LSPhaseHandlerRegister<ValidatePhaseState>(dispatcher);

// v4
var register = new LSPhaseHandlerRegister<ValidatePhaseState>();
```

#### Event Creation

```csharp
// v3
var event = new MyEvent(dispatcher, data);

// v4
var options = new LSEventOptions(dispatcher);
var event = new MyEvent(options, data);
```

#### State Initialization

```csharp
// v3
state.Initialize(dispatcher);

// v4
var options = new LSEventOptions(dispatcher);
state.Initialize(options);
```

## 📚 Examples

### Complete E-commerce Order Processing

```csharp
public class OrderEvent : LSEvent {
    public int OrderId { get; }
    public decimal Amount { get; }
    public int CustomerId { get; }
    
    public OrderEvent(LSEventOptions options, int orderId, decimal amount, int customerId) 
        : base(options) {
        OrderId = orderId;
        Amount = amount;
        CustomerId = customerId;
        
        SetData("order_id", orderId);
        SetData("amount", amount);
        SetData("customer_id", customerId);
    }
}

// Register processing pipeline
var dispatcher = LSDispatcher.Singleton;

// Validation
dispatcher.ForEventPhase<OrderEvent, BusinessState.ValidatePhaseState>(
    register => register
        .WithPriority(LSPriority.CRITICAL)
        .Handler(ctx => {
            var amount = ctx.Event.GetData<decimal>("amount");
            if (amount <= 0) {
                ctx.Event.SetData("error", "Invalid amount");
                return HandlerProcessResult.CANCELLED;
            }
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Configuration
dispatcher.ForEventPhase<OrderEvent, BusinessState.ConfigurePhaseState>(
    register => register
        .Handler(ctx => {
            var orderId = ctx.Event.GetData<int>("order_id");
            var reservationId = ReserveInventory(orderId);
            ctx.Event.SetData("reservation_id", reservationId);
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Execution
dispatcher.ForEventPhase<OrderEvent, BusinessState.ExecutePhaseState>(
    register => register
        .Handler(ctx => {
            var customerId = ctx.Event.GetData<int>("customer_id");
            var amount = ctx.Event.GetData<decimal>("amount");
            
            // Process payment asynchronously
            var paymentId = ProcessPayment(customerId, amount);
            ctx.Event.SetData("payment_id", paymentId);
            
            // Payment processing would resume via external callback
            return HandlerProcessResult.WAITING;
        })
        .Build());

// Cleanup
dispatcher.ForEventPhase<OrderEvent, BusinessState.CleanupPhaseState>(
    register => register
        .Handler(ctx => {
            // Always cleanup, regardless of success/failure
            if (ctx.Event.TryGetData("reservation_id", out int reservationId)) {
                ReleaseReservation(reservationId);
            }
            return HandlerProcessResult.SUCCESS;
        })
        .Build());

// Success handling
dispatcher.ForEventState<OrderEvent, SucceedState>(
    register => register
        .Handler(evt => {
            var orderId = evt.GetData<int>("order_id");
            SendConfirmationEmail(orderId);
        })
        .Build());

// Usage
var options = new LSEventOptions(dispatcher);
var order = new OrderEvent(options, 12345, 99.99m, 67890);
var result = order.Dispatch();
```

### Graph Pathfinding

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
- Use semantic versioning for releases

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with ❤️ for the .NET community
- Inspired by enterprise event-driven architectures
- Designed for game development and business applications

---

**LSUtils** - Making .NET development more productive, one utility at a time! 🚀
