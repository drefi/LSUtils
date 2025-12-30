# Graphs

Módulo de estruturas de grafos genéricas para modelagem de relações e pathfinding.

## Visão Geral

O módulo fornece implementações de grafos direcionados genéricos, com suporte para:

- Grafos baseados em lista de adjacência
- Adaptadores para converter grids em grafos
- Algoritmos de pathfinding (A*, Dijkstra, etc.)
- Representação de arestas com custos

## Arquivos

- **IGraph.cs**: Interface base para grafos genéricos
- **Edge.cs**: Record imutável representando uma aresta com custo
- **AdjacencyListGraph.cs**: Implementação de grafo usando lista de adjacência
- **GridGraphAdapter.cs**: Adaptador para converter grids em grafos
- **Algorithms/GraphAlgorithms.cs**: Algoritmos de grafos (A*, etc.)

## Uso Básico

### AdjacencyListGraph

```csharp
using LSUtils.Graphs;

// Criar grafo de strings
var graph = new AdjacencyListGraph<string>();

// Adicionar nós
graph.AddNode("A");
graph.AddNode("B");
graph.AddNode("C");

// Adicionar arestas direcionadas
graph.AddEdge("A", "B"); // A -> B
graph.AddEdge("A", "C"); // A -> C
graph.AddEdge("B", "C"); // B -> C

// Verificar se nó existe
bool hasNode = graph.HasNode("A"); // true

// Obter vizinhos
foreach (var neighbor in graph.GetNeighbors("A")) {
    Console.WriteLine($"A conecta-se a {neighbor}");
    // Output: A conecta-se a B
    //         A conecta-se a C
}

// Obter todos os nós
foreach (var node in graph.Nodes) {
    Console.WriteLine($"Nó: {node}");
}

// Remover aresta
graph.RemoveEdge("A", "B"); // Remove A -> B

// Remover nó (remove todas as arestas relacionadas)
graph.RemoveNode("C"); // Remove C e todas as arestas para/de C
```

### Edge (Aresta)

```csharp
using LSUtils.Graphs;

// Criar arestas com custos
var edge1 = new Edge<string>("A", "B", 10.5f);
var edge2 = new Edge<string>("B", "C", 5.0f);
var edge3 = new Edge<string>("A", "B"); // Custo padrão = 1.0

// Acessar propriedades
string from = edge1.From; // "A"
string to = edge1.To; // "B"
float cost = edge1.Cost; // 10.5

// Value equality (records)
var edge4 = new Edge<string>("A", "B", 10.5f);
bool equal = edge1 == edge4; // true
```

### GridGraphAdapter

```csharp
using LSUtils.Graphs;
using LSUtils.Grids;

// Criar grid
var grid = new DenseGrid<int>(10, 10);

// Preencher grid com valores (0 = livre, 1 = obstáculo)
grid.SetCell(new GridPosition(5, 5), 1);

// Adaptar grid para grafo
var graphAdapter = new GridGraphAdapter<GridPosition, int>(grid, includeDiagonals: false);

// Agora pode usar como grafo
bool hasNode = graphAdapter.HasNode(new GridPosition(5, 5)); // true

// Obter vizinhos (apenas posições válidas do grid)
var neighbors = graphAdapter.GetNeighbors(new GridPosition(3, 3));
// Retorna até 4 vizinhos (N, S, E, W)

// Com diagonais
var graphWithDiagonals = new GridGraphAdapter<GridPosition, int>(grid, includeDiagonals: true);
var allNeighbors = graphWithDiagonals.GetNeighbors(new GridPosition(3, 3));
// Retorna até 8 vizinhos
```

## Algoritmos

### Delegates para Pathfinding

```csharp
using LSUtils.Graphs.Algorithms;

// Delegate para cálculo de distância entre nós
public delegate float NodeDistanceFunc<TNode>(TNode from, TNode to);

// Exemplo de heurística (distância Manhattan para grids)
NodeDistanceFunc<GridPosition> manhattanDistance = (from, to) => {
    return Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
};

// Exemplo de função de custo
NodeDistanceFunc<GridPosition> unitCost = (from, to) => 1.0f;
```

### A* (Planejado)

```csharp
// Uso futuro do A*
var graph = new GridGraphAdapter<GridPosition, int>(grid);
var start = new GridPosition(0, 0);
var goal = new GridPosition(9, 9);

// var path = GraphAlgorithms.AStar(
//     graph,
//     start,
//     goal,
//     manhattanDistance, // heurística
//     unitCost           // custo de movimento
// );
```

## Exemplos Práticos

### Sistema de Rotas

```csharp
// Criar grafo de cidades
var routes = new AdjacencyListGraph<string>();

routes.AddEdge("São Paulo", "Rio de Janeiro");
routes.AddEdge("São Paulo", "Belo Horizonte");
routes.AddEdge("Rio de Janeiro", "Belo Horizonte");
routes.AddEdge("Belo Horizonte", "Brasília");

// Verificar conexões diretas
bool canGoDirect = routes.GetNeighbors("São Paulo")
    .Contains("Rio de Janeiro"); // true
```

### Sistema de Dependências

```csharp
// Grafo de dependências de tarefas
var tasks = new AdjacencyListGraph<int>();

// Tarefa 1 deve ser feita antes de 2 e 3
tasks.AddEdge(1, 2);
tasks.AddEdge(1, 3);

// Tarefa 2 deve ser feita antes de 4
tasks.AddEdge(2, 4);

// Tarefa 3 deve ser feita antes de 4
tasks.AddEdge(3, 4);

// Obter tarefas que dependem da tarefa 1
var dependents = tasks.GetNeighbors(1); // [2, 3]
```

### Pathfinding em Mapa de Jogo

```csharp
// Criar mapa 50x50
var map = new DenseGrid<bool>(50, 50);

// Marcar obstáculos (true = bloqueado)
map.SetCell(new GridPosition(25, 25), true);
map.SetCell(new GridPosition(25, 26), true);

// Converter para grafo
var pathfindingGraph = new GridGraphAdapter<GridPosition, bool>(
    map,
    includeDiagonals: false
);

// Filtrar nós bloqueados ao buscar caminho
bool IsWalkable(GridPosition pos) {
    return map.GetCell(pos) == false; // false = livre
}

// Usar com A* para encontrar caminho
// var path = GraphAlgorithms.AStar(...);
```

### Grafo Social

```csharp
public class Person {
    public string Name { get; set; }
    public override bool Equals(object obj) => obj is Person p && p.Name == Name;
    public override int GetHashCode() => Name.GetHashCode();
}

var socialNetwork = new AdjacencyListGraph<Person>();

var alice = new Person { Name = "Alice" };
var bob = new Person { Name = "Bob" };
var charlie = new Person { Name = "Charlie" };

// Alice é amiga de Bob e Charlie
socialNetwork.AddEdge(alice, bob);
socialNetwork.AddEdge(alice, charlie);

// Bob é amigo de Charlie
socialNetwork.AddEdge(bob, charlie);

// Encontrar amigos de Alice
var friends = socialNetwork.GetNeighbors(alice);
```

## Performance

### AdjacencyListGraph*

- **Adicionar nó**: O(1)
- **Adicionar aresta**: O(1) médio
- **Remover nó**: O(V + E) onde V = nós, E = arestas
- **Obter vizinhos**: O(k) onde k = número de vizinhos
- **Verificar se nó existe**: O(1)
- **Memória**: O(V + E)

### GridGraphAdapter*

- **Herda performance do grid subjacente**
- **GetNeighbors**: O(1) - no máximo 8 verificações
- **HasNode**: O(1) - verificação de bounds
- **Memória**: O(1) - apenas referência ao grid

## Características

### Grafos Direcionados

Todos os grafos são **direcionados por padrão**:

```csharp
graph.AddEdge("A", "B"); // A -> B

// B não conecta de volta para A automaticamente
var neighbors = graph.GetNeighbors("B"); // vazio
```

Para grafos não-direcionados, adicione arestas bidirecionais:

```csharp
graph.AddEdge("A", "B");
graph.AddEdge("B", "A"); // Bidirecional
```

### Tipos Genéricos

Grafos funcionam com qualquer tipo:

```csharp
// Grafo de inteiros
var intGraph = new AdjacencyListGraph<int>();

// Grafo de tipos customizados
var customGraph = new AdjacencyListGraph<MyCustomType>();

// GridGraphAdapter com qualquer tipo de dado
var gridGraph = new GridGraphAdapter<GridPosition, MyTileData>(grid);
```

## Thread Safety

⚠️ **Implementações não são thread-safe.** Use locks externos para acesso concorrente:

```csharp
var graph = new AdjacencyListGraph<string>();
var lockObj = new object();

lock (lockObj) {
    graph.AddEdge("A", "B");
}
```

## Integração com Grids

Grids podem ser convertidos em grafos para pathfinding:

```csharp
using LSUtils.Grids;
using LSUtils.Graphs;

// 1. Criar grid
var grid = new DenseGrid<int>(100, 100);

// 2. Converter para grafo
var graph = new GridGraphAdapter<GridPosition, int>(grid);

// 3. Usar algoritmos de grafo no grid
// var path = GraphAlgorithms.AStar(graph, start, goal, heuristic, cost);
```

## Ver Também

- [Grids](../Grids/README.md) - Estruturas de grids compatíveis
- [Testes](../../tests/Graphs/) - Exemplos de uso nos testes unitários
