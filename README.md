# LSUtils

A modular .NET utility library with components for processing, logging, graphs, and more.

## ✨ Features

- 🔄 **Process System**: Flexible process and behaviour tree system with async operations support
- 📝 **Logging**: Multi-provider logging with hierarchical context
- 🗺️ **Graphs**: Graph implementations (Grid, Hex, Node) with A* and Dijkstra pathfinding
- 📦 **Collections**: Specialized data structures (BinaryHeap, CachePool)
- 🎲 **Random**: High-quality Lehmer random number generator
- 🔷 **Hex**: Complete hexagonal coordinate system
- 🌍 **Localization**: Multi-language localization and formatting support
- 🔧 **Core Utilities**: Essential interfaces, types, math, and utilities

## 📦 Installation

Add manually to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="LSUtils" Version="1.0.0" />
</ItemGroup>
```

## 🚀 Quick Start

### Process System

Build complex processes with a fluent API:

```csharp
using LSUtils.ProcessSystem;

var process = LSProcess.Create("patrol", builder => builder
    .Sequence("main", seq => seq
        .Handler("move-to-point-a", () => MoveToPointA())
        .Handler("wait", () => Wait(2.0f))
        .Handler("move-to-point-b", () => MoveToPointB())
        .Handler("wait", () => Wait(2.0f))
    )
);

// Run the process
var manager = new LSProcessManager();
manager.AddProcess(process);
manager.Tick(deltaTime);
```

### Logging System

Logging with levels and context:

```csharp
using LSUtils.Logging;

var logger = new LSLogger("MyApp");

logger.Info("Application started");
logger.Debug("Loading configuration...", new { Config = "app.json" });
logger.Warning("Cache miss for key: {key}", "user:123");
logger.Error("Failed to connect to database", exception);
```

### Graph Pathfinding

Find paths in graphs with A* or Dijkstra:

```csharp
using LSUtils.Graphs;

var graph = new GridGraph(width: 100, height: 100);
var pathResolver = new AStarPathResolver<GridNode>();

var path = pathResolver.FindPath(
    graph,
    startNode,
    endNode,
    (current, neighbor) => Vector2.Distance(current.Position, neighbor.Position)
);
```

### Collections

Use optimized data structures:

```csharp
using LSUtils.Collections;

// Binary Heap for prioritization
var heap = new BinaryHeap<int>();
heap.Insert(5);
heap.Insert(3);
var min = heap.ExtractMin(); // 3

// Cache Pool for object reuse
var pool = new CachePool<MyObject>(() => new MyObject());
var obj = pool.Get();
// ... use obj
pool.Return(obj);
```

## 📖 Documentation

### Main Guides

- **[Getting Started](docs/getting-started.md)** - Start here to learn the basics
- **[Process System Guide](docs/guides/process-system-guide.md)** - Complete guide to the process system
- **[Logging Guide](docs/guides/logging-guide.md)** - Configure and use the logging system
- **[Graph Guide](docs/guides/graph-guide.md)** - Work with graphs and pathfinding

### API Reference

- [Core](docs/api-reference/core.md) - Core interfaces, types, and utilities
- [Collections](docs/api-reference/collections.md) - Data structures
- [Graphs](docs/api-reference/graphs.md) - Graph system
- [Process System](docs/api-reference/process-system.md) - Full Process System API
- [Logging](docs/api-reference/logging.md) - Logging API

### Examples

See [docs/examples/](docs/examples/) for detailed usage examples.

## 🏗️ Project Structure

```file tree
LSUtils/
├── src/                      # Source code
│   ├── Collections/          # Data structures
│   ├── Exceptions/           # Custom exceptions
│   ├── Graphs/               # Graph system
│   ├── Hex/                  # Hex coordinates
│   ├── JsonConverters/       # JSON converters
│   ├── Locale/               # Localization
│   ├── Logging/              # Logging system
│   └── ProcessSystem/        # Process system
├── docs/                     # Documentation
└── tests/                    # Tests (future)
```

## 🔧 Requirements

- **.NET 8.0** or higher

## 🧪 Tests

```bash
dotnet test
```

## 🤝 Contributing

Sure, go ahead.

1. Fork the project
2. Create a branch for your feature (`git checkout -b feature/MyFeature`)
3. Commit your changes (`git commit -m 'Add MyFeature'`)
4. Push to the branch (`git push origin feature/MyFeature`)
5. Open a Pull Request

Read [CONTRIBUTING.md](CONTRIBUTING.md) for more details.

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for the change history.

## 📄 License

This project is not under any license. Do whatever you want.

## 👤 Author

- GitHub: [@drefi](https://github.com/drefi)

## 🙏 Thanks

- My mom for putting up with me.