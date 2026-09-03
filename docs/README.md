# Documentação LSUtils

Bem-vindo à documentação completa do LSUtils!

## 📚 Índice

### Primeiros Passos

- **[Getting Started](getting-started.md)** - Comece aqui para aprender o básico

### Guias de Usuário

- **[Process System Guide](guides/process-system-guide.md)** - Guia completo do sistema de processos
- **[Logging Guide](guides/logging-guide.md)** - Configure e use o sistema de logging
- **[Graph Guide](guides/graph-guide.md)** - Trabalhe com grafos e pathfinding
- **[Terrain World Guide](guides/terrain-world-guide.md)** - Modele terrenos, conteudos, regioes e biomas sem grid

### Referência da API

- **[Core](api-reference/core.md)** - Interfaces, tipos e utilitários core
- **[Collections](api-reference/collections.md)** - Estruturas de dados especializadas
- **[Graphs](api-reference/graphs.md)** - Sistema de grafos e pathfinding
- **[Process System](api-reference/process-system.md)** - API completa do Process System
- **[Logging](api-reference/logging.md)** - API de logging

### Exemplos Práticos

- **[Process System Examples](examples/process-system-examples.md)** - Exemplos do sistema de processos
- **[Logging Examples](examples/logging-examples.md)** - Exemplos de logging
- **[Graph Examples](examples/graph-examples.md)** - Exemplos de grafos
- **[Terrain World Examples](examples/terrain-world-examples.md)** - Exemplos de terrenos e regioes

## 🎯 Por Onde Começar?

### Se você é novo no LSUtils

Comece com [Getting Started](getting-started.md) para entender os conceitos básicos.

### Se você quer usar um componente específico

- **Process System**: [Process System Guide](guides/process-system-guide.md)
- **Logging**: [Logging Guide](guides/logging-guide.md)
- **Grafos**: [Graph Guide](guides/graph-guide.md)

### Se você precisa de referência técnica

Veja a seção [Referência da API](#referência-da-api) acima.

## 🔍 Busca Rápida

### Conceitos Principais

#### Process System

Sistema de árvores de execução para operações com pontos de intervenção:

- Nós: Sequence, Selector, Handler e Inverter; condições controlam elegibilidade
- Callbacks registrados via ProcessManager para observar dados e controlar resultados
- Espera explícita com WAITING e continuação via Resume/Fail, sem agendador de threads
- Gerenciamento de prioridades
- Contexto multi-nível

Consulte o [guia atual do ProcessSystem](../src/ProcessSystem/QUICK_GUIDE.md).

#### Logging

Sistema de logging multi-provider com níveis e contexto:

- Níveis: Trace, Debug, Info, Warning, Error, Fatal
- Múltiplos providers
- Contexto hierárquico

#### Graphs

Grafos com pathfinding A* e Dijkstra:

- GridGraph, HexGraph, NodeGraph
- PathResolvers configuráveis
- Heurísticas customizáveis

#### Collections

Estruturas de dados otimizadas:

- BinaryHeap: heap binário mínimo
- CachePool: pool de objetos reutilizáveis

## 📖 Estrutura da Documentação

```file tree
docs/
├── README.md (este arquivo)
├── getting-started.md
├── api-reference/
│   ├── core.md
│   ├── collections.md
│   ├── graphs.md
│   ├── process-system.md
│   └── logging.md
├── guides/
│   ├── process-system-guide.md
│   ├── logging-guide.md
│   └── graph-guide.md
└── examples/
    ├── process-system-examples.md
    ├── logging-examples.md
    └── graph-examples.md
```

## 🤝 Contribuindo com a Documentação

Encontrou um erro ou quer melhorar a documentação? Contribuições são bem-vindas!

1. Faça fork do projeto
2. Crie uma branch para sua melhoria
3. Faça commit das mudanças
4. Abra um Pull Request

## 📞 Suporte

- **Issues**: [GitHub Issues](https://github.com/yourusername/LSUtils/issues)
- **Discussões**: [GitHub Discussions](https://github.com/yourusername/LSUtils/discussions)

---

**Última Atualização**: 29/12/2025
