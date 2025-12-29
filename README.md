# LSUtils

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/yourusername/LSUtils)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

Uma biblioteca .NET utilitária modular com componentes para processamento, logging, grafos e mais.

## ✨ Características

- 🔄 **Process System**: Sistema flexível de processos e behaviour trees com suporte a operações assíncronas
- 📝 **Logging**: Sistema de logging multi-provider com suporte a contexto hierárquico
- 🗺️ **Graphs**: Implementações de grafos (Grid, Hex, Node) com pathfinding A* e Dijkstra
- 📦 **Collections**: Estruturas de dados especializadas (BinaryHeap, CachePool)
- 🎲 **Random**: Gerador Lehmer de números aleatórios de alta qualidade
- 🔷 **Hex**: Sistema completo de coordenadas hexagonais
- 🌍 **Localization**: Suporte a localização e formatação multi-idioma
- 🔧 **Core Utilities**: Interfaces, tipos, matemática e utilitários essenciais

## 📦 Instalação

```bash
dotnet add package LSUtils
```

Ou adicione manualmente ao seu `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="LSUtils" Version="1.0.0" />
</ItemGroup>
```

## 🚀 Quick Start

### Process System

Crie processos complexos com uma API fluente:

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

// Execute o processo
var manager = new LSProcessManager();
manager.AddProcess(process);
manager.Tick(deltaTime);
```

### Logging System

Sistema de logging com níveis e contexto:

```csharp
using LSUtils.Logging;

var logger = new LSLogger("MyApp");

logger.Info("Application started");
logger.Debug("Loading configuration...", new { Config = "app.json" });
logger.Warning("Cache miss for key: {key}", "user:123");
logger.Error("Failed to connect to database", exception);
```

### Graph Pathfinding

Encontre caminhos em grafos com A* ou Dijkstra:

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

Use estruturas de dados otimizadas:

```csharp
using LSUtils.Collections;

// Binary Heap para priorização
var heap = new BinaryHeap<int>();
heap.Insert(5);
heap.Insert(3);
var min = heap.ExtractMin(); // 3

// Cache Pool para reutilização de objetos
var pool = new CachePool<MyObject>(() => new MyObject());
var obj = pool.Get();
// ... use obj
pool.Return(obj);
```

## 📖 Documentação

### Guias Principais

- **[Getting Started](docs/getting-started.md)** - Comece aqui para aprender o básico
- **[Process System Guide](docs/guides/process-system-guide.md)** - Guia completo do sistema de processos
- **[Logging Guide](docs/guides/logging-guide.md)** - Configure e use o sistema de logging
- **[Graph Guide](docs/guides/graph-guide.md)** - Trabalhe com grafos e pathfinding

### Referência da API

- [Core](docs/api-reference/core.md) - Interfaces, tipos e utilitários core
- [Collections](docs/api-reference/collections.md) - Estruturas de dados
- [Graphs](docs/api-reference/graphs.md) - Sistema de grafos
- [Process System](docs/api-reference/process-system.md) - API completa do Process System
- [Logging](docs/api-reference/logging.md) - API de logging

### Exemplos

Veja [docs/examples/](docs/examples/) para exemplos detalhados de uso.

## 🏗️ Estrutura do Projeto

```file tree
LSUtils/
├── src/                      # Código fonte
│   ├── Collections/          # Estruturas de dados
│   ├── Exceptions/           # Exceções customizadas
│   ├── Graphs/              # Sistema de grafos
│   ├── Hex/                 # Coordenadas hexagonais
│   ├── JsonConverters/      # Conversores JSON
│   ├── Locale/              # Localização
│   ├── Logging/             # Sistema de logging
│   └── ProcessSystem/       # Sistema de processos
├── docs/                    # Documentação
└── tests/                   # Testes (futuramente)
```

## 🔧 Requisitos

- **.NET 8.0** ou superior
- **C# 11.0** ou superior

## 🧪 Testes

```bash
dotnet test
```

## 🤝 Contribuindo

Contribuições são bem-vindas! Por favor:

1. Faça fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/MinhaFeature`)
3. Commit suas mudanças (`git commit -m 'Adiciona MinhaFeature'`)
4. Push para a branch (`git push origin feature/MinhaFeature`)
5. Abra um Pull Request

Leia [CONTRIBUTING.md](CONTRIBUTING.md) para mais detalhes.

## 📝 Changelog

Veja [CHANGELOG.md](CHANGELOG.md) para histórico de mudanças.

## 📄 Licença

Este projeto está sob a licença MIT. Veja [LICENSE](LICENSE) para mais detalhes.

## 👤 Autor

- GitHub: [@drefi](https://github.com/drefi)

## 🙏 Agradecimentos

- Comunidade .NET
- Contribuidores do projeto

---

Feito com ❤️ pela comunidade LSUtils
