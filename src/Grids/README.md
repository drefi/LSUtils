# Grids

Módulo de estruturas de grids 2D genéricas para representação espacial.

## Visão Geral

O módulo fornece implementações de grids 2D com diferentes estratégias de armazenamento:

- **DenseGrid**: Grid denso baseado em array contíguo (ideal para grids pequenos/médios totalmente preenchidos)
- **SparseGrid**: Grid esparso baseado em dicionário (ideal para grids grandes com muitos espaços vazios)

## Arquivos

- **IGrid.cs**: Interface base para grids 2D genéricos
- **IGridPosition.cs**: Interface para posições em grids
- **GridPosition.cs**: Struct imutável representando uma posição (x, y)
- **GridCell.cs**: Classe auxiliar para células com posição e dados
- **DenseGrid.cs**: Implementação densa usando array
- **SparseGrid.cs**: Implementação esparsa usando dicionário

## Uso Básico

### DenseGrid

```csharp
using LSUtils.Grids;

// Criar grid denso 10x10
var grid = new DenseGrid<int>(10, 10);

// Definir valores
grid.SetCell(new GridPosition(5, 5), 42);
grid.SetCell(new GridPosition(3, 7), 100);

// Obter valores
int value = grid.GetCell(new GridPosition(5, 5)); // 42

// Verificar posições válidas
bool valid = grid.IsValidPosition(new GridPosition(15, 15)); // false

// Buscar vizinhos (4 direções cardeais)
foreach (var neighbor in grid.GetNeighbors(new GridPosition(5, 5))) {
    Console.WriteLine($"Vizinho: {neighbor.X}, {neighbor.Y}");
}

// Buscar vizinhos incluindo diagonais (8 direções)
foreach (var neighbor in grid.GetNeighbors(new GridPosition(5, 5), includeDiagonals: true)) {
    Console.WriteLine($"Vizinho diagonal: {neighbor.X}, {neighbor.Y}");
}

// Iterar sobre todas as posições
foreach (var pos in grid.GetAllPositions()) {
    var value = grid.GetCell(pos);
    // Processar célula...
}

// Limpar grid
grid.Clear();
```

### SparseGrid

```csharp
using LSUtils.Grids;

// Criar grid esparso muito grande (eficiente em memória)
var grid = new SparseGrid<string>(10000, 10000);

// Definir apenas algumas células
grid.SetCell(new GridPosition(100, 200), "Item A");
grid.SetCell(new GridPosition(5000, 5000), "Item B");

// Obter valores (células não definidas retornam default)
string? value = grid.GetCell(new GridPosition(100, 200)); // "Item A"
string? empty = grid.GetCell(new GridPosition(0, 0)); // null

// Mesmas operações de vizinhos, validação, etc.
var neighbors = grid.GetNeighbors(new GridPosition(100, 200));
```

### GridPosition

```csharp
using LSUtils.Grids;

// Criar posições
var pos1 = new GridPosition(5, 10);
var pos2 = new GridPosition(5, 10);

// Value equality (structs)
bool equal = pos1 == pos2; // true

// Usar como chave em dicionários
var dict = new Dictionary<GridPosition, string>();
dict[new GridPosition(1, 2)] = "treasure";
```

### GridCell

```csharp
using LSUtils.Grids;

// Criar célula com posição e dados
var cell = new GridCell<GridPosition, int>(new GridPosition(3, 4), 42);

// Acessar propriedades
var pos = cell.Position; // GridPosition(3, 4)
var data = cell.Data; // 42

// Modificar dados
cell.Data = 100;
```

## Quando Usar Cada Implementação

### DenseGrid*

✅ **Use quando:**

- Grid é pequeno/médio (< 1000x1000)
- Maioria das células contém dados
- Precisa de acesso muito rápido (O(1) direto no array)
- Iteração frequente sobre todas as células

❌ **Evite quando:**

- Grid é muito grande com poucas células ocupadas
- Memória é limitada

### SparseGrid*

✅ **Use quando:**

- Grid é muito grande (10000x10000+)
- Poucas células contêm dados
- Padrão de ocupação é esparso/disperso
- Memória é limitada

❌ **Evite quando:**

- Grid é pequeno e denso (overhead do dicionário)
- Precisa de máxima performance de acesso

## Integração com Graphs

Grids podem ser facilmente convertidos em grafos para pathfinding:

```csharp
using LSUtils.Grids;
using LSUtils.Graphs;

// Criar grid
var grid = new DenseGrid<bool>(50, 50);

// Marcar obstáculos
grid.SetCell(new GridPosition(10, 10), true); // true = obstáculo

// Adaptar para grafo
var graph = new GridGraphAdapter<GridPosition, bool>(grid, includeDiagonals: false);

// Usar com algoritmos de pathfinding
// var path = GraphAlgorithms.AStar(graph, start, goal, heuristic, cost);
```

## Exemplos Práticos

### Sistema de Mapa de Jogo

```csharp
// Criar mapa 100x100
var map = new DenseGrid<TileType>(100, 100);

// Preencher terreno
for (int x = 0; x < 100; x++) {
    for (int y = 0; y < 100; y++) {
        map.SetCell(new GridPosition(x, y), TileType.Grass);
    }
}

// Adicionar obstáculos
map.SetCell(new GridPosition(50, 50), TileType.Wall);

// Verificar se pode mover para posição
bool CanMoveTo(GridPosition pos) {
    if (!map.IsValidPosition(pos)) return false;
    var tile = map.GetCell(pos);
    return tile != TileType.Wall;
}
```

### Sistema de Inventário 2D

```csharp
// Inventário como grid esparso
var inventory = new SparseGrid<Item>(10, 5);

// Adicionar itens
inventory.SetCell(new GridPosition(0, 0), new Item("Sword"));
inventory.SetCell(new GridPosition(2, 1), new Item("Potion"));

// Verificar slot vazio
bool IsSlotEmpty(GridPosition pos) {
    return inventory.GetCell(pos) == null;
}
```

## Performance

### DenseGrid**

- **Acesso**: O(1)
- **Memória**: O(width × height)
- **Iteração**: O(width × height)

### SparseGrid**

- **Acesso**: O(1) médio (dicionário)
- **Memória**: O(células ocupadas)
- **Iteração**: O(width × height) para GetAllPositions(), O(células ocupadas) para células definidas

## Thread Safety

⚠️ **Nenhuma das implementações é thread-safe por padrão.** Use locks externos se precisar de acesso concorrente:

```csharp
var grid = new DenseGrid<int>(10, 10);
var lockObj = new object();

lock (lockObj) {
    grid.SetCell(pos, value);
}
```

## Ver Também

- [Graphs](../Graphs/README.md) - Integração com grafos para pathfinding
- [Testes](../../tests/Grids/) - Exemplos de uso nos testes unitários
