# Spatial

Módulo de estruturas de indexação espacial para consultas e operações eficientes em espaço 2D.

## Visão Geral

O módulo fornece estruturas de dados para particionamento espacial hierárquico:

- **QuadTree**: Árvore quaternária para particionamento 2D recursivo
- **Bounds**: Record para representação de limites retangulares
- **ISpatialIndex**: Interface genérica para índices espaciais

## Arquivos

- **ISpatialIndex.cs**: Interface base para estruturas de indexação espacial
- **Bounds.cs**: Struct para limites retangulares com operações de contenção e interseção
- **QuadTree.cs**: Implementação de árvore quaternária

## Uso Básico

### QuadTree

```csharp
using LSUtils.Spatial;

// Criar QuadTree cobrindo área de -500 a 500 em X e Y
var quadTree = new QuadTree<string>(
    bounds: new Bounds(0, 0, 1000, 1000),
    capacity: 4  // Máximo de objetos por nó antes de subdividir
);

// Inserir objetos com suas posições
quadTree.Insert("Player", new Bounds(100, 100, 10, 10));
quadTree.Insert("Enemy1", new Bounds(150, 120, 8, 8));
quadTree.Insert("Enemy2", new Bounds(-200, -150, 8, 8));
quadTree.Insert("Treasure", new Bounds(105, 95, 5, 5));

// Consultar objetos em uma área
var searchArea = new Bounds(100, 100, 50, 50);
var nearbyObjects = quadTree.Query(searchArea);
// Retorna: ["Player", "Enemy1", "Treasure"]

// Remover objeto
bool removed = quadTree.Remove("Enemy1");

// Limpar todos os objetos
quadTree.Clear();

// Verificar contagem
int count = quadTree.Count;
```

### Bounds

```csharp
using LSUtils.Spatial;

// Criar bounds (centro X, centro Y, largura, altura)
var bounds1 = new Bounds(0, 0, 100, 100);

// Propriedades calculadas
float minX = bounds1.MinX; // -50
float maxX = bounds1.MaxX; // 50
float minY = bounds1.MinY; // -50
float maxY = bounds1.MaxY; // 50

// Verificar se contém ponto
bool containsPoint = bounds1.Contains(10, 20); // true

// Verificar se contém outro bounds completamente
var inner = new Bounds(0, 0, 50, 50);
bool containsBounds = bounds1.Contains(inner); // true

// Verificar interseção com outro bounds
var bounds2 = new Bounds(40, 40, 100, 100);
bool intersects = bounds1.Intersects(bounds2); // true

// Value equality
var bounds3 = new Bounds(0, 0, 100, 100);
bool equal = bounds1 == bounds3; // true
```

## Exemplos Práticos

### Sistema de Colisão em Jogo

```csharp
public class GameObject
{
    public string Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    
    public Bounds GetBounds() => new Bounds(X, Y, Width, Height);
}

// Criar QuadTree para mundo de jogo 2000x2000
var collisionSystem = new QuadTree<GameObject>(
    new Bounds(0, 0, 2000, 2000),
    capacity: 8
);

// Adicionar objetos
var player = new GameObject { Id = "Player", X = 100, Y = 100, Width = 32, Height = 32 };
var enemy = new GameObject { Id = "Enemy", X = 150, Y = 120, Width = 32, Height = 32 };

collisionSystem.Insert(player, player.GetBounds());
collisionSystem.Insert(enemy, enemy.GetBounds());

// Detectar colisões próximas ao jogador
var checkArea = new Bounds(player.X, player.Y, 100, 100);
var nearbyObjects = collisionSystem.Query(checkArea);

foreach (var obj in nearbyObjects)
{
    if (obj != player && player.GetBounds().Intersects(obj.GetBounds()))
    {
        Console.WriteLine($"Colisão detectada com {obj.Id}!");
    }
}
```

### Sistema de Visibilidade/Culling

```csharp
public class RenderableEntity
{
    public string Name { get; set; }
    public Bounds ScreenBounds { get; set; }
    public void Render() { /* ... */ }
}

var sceneTree = new QuadTree<RenderableEntity>(
    new Bounds(0, 0, 1920, 1080),
    capacity: 16
);

// Adicionar entidades renderizáveis
sceneTree.Insert(entity1, entity1.ScreenBounds);
sceneTree.Insert(entity2, entity2.ScreenBounds);
// ... adicionar mais entidades

// Renderizar apenas objetos visíveis na câmera
var cameraView = new Bounds(960, 540, 1920, 1080);
var visibleEntities = sceneTree.Query(cameraView);

foreach (var entity in visibleEntities)
{
    entity.Render();
}
```

### Sistema de IA - Busca de Vizinhos

```csharp
public class AIAgent
{
    public string Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float DetectionRadius { get; set; }
}

var aiSystem = new QuadTree<AIAgent>(
    new Bounds(0, 0, 10000, 10000),
    capacity: 10
);

// Adicionar agentes
foreach (var agent in agents)
{
    aiSystem.Insert(agent, new Bounds(agent.X, agent.Y, 1, 1));
}

// Encontrar vizinhos de um agente específico
var currentAgent = agents[0];
var searchRadius = currentAgent.DetectionRadius;
var searchArea = new Bounds(
    currentAgent.X,
    currentAgent.Y,
    searchRadius * 2,
    searchRadius * 2
);

var nearbyAgents = aiSystem.Query(searchArea);
Console.WriteLine($"Agente {currentAgent.Id} detectou {nearbyAgents.Count - 1} vizinhos");
```

### Sistema de Gerenciamento de Partículas

```csharp
public class Particle
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Lifetime { get; set; }
}

var particleSystem = new QuadTree<Particle>(
    new Bounds(0, 0, 5000, 5000),
    capacity: 20
);

// Adicionar milhares de partículas eficientemente
for (int i = 0; i < 10000; i++)
{
    var particle = new Particle
    {
        Id = i,
        X = Random.Shared.NextSingle() * 5000 - 2500,
        Y = Random.Shared.NextSingle() * 5000 - 2500
    };
    particleSystem.Insert(particle, new Bounds(particle.X, particle.Y, 1, 1));
}

// Consultar partículas em uma região específica
var screenArea = new Bounds(0, 0, 800, 600);
var visibleParticles = particleSystem.Query(screenArea);
Console.WriteLine($"Partículas visíveis: {visibleParticles.Count}/{particleSystem.Count}");
```

### Sistema de Mapa com Objetos Interativos

```csharp
public class InteractiveObject
{
    public string Type { get; set; } // "Item", "NPC", "Door", etc.
    public Bounds Area { get; set; }
    public void OnInteract() { /* ... */ }
}

var worldMap = new QuadTree<InteractiveObject>(
    new Bounds(0, 0, 50000, 50000),
    capacity: 6
);

// Adicionar objetos do mundo
worldMap.Insert(new InteractiveObject
{
    Type = "Chest",
    Area = new Bounds(1234, 5678, 20, 20)
}, new Bounds(1234, 5678, 20, 20));

// Quando jogador interage, buscar objetos próximos
var playerPos = new Bounds(1230, 5680, 32, 32);
var interactRadius = new Bounds(playerPos.X, playerPos.Y, 64, 64);
var nearbyInteractables = worldMap.Query(interactRadius);

foreach (var obj in nearbyInteractables)
{
    if (playerPos.Intersects(obj.Area))
    {
        obj.OnInteract();
    }
}
```

## Performance

### QuadTree*

- **Inserção**: O(log n) médio, O(n) pior caso (objetos todos na mesma posição)
- **Consulta**: O(log n + k) onde k = número de resultados
- **Remoção**: O(log n) médio
- **Memória**: O(n) onde n = número de objetos
- **Subdivisão**: Ocorre automaticamente quando capacity é excedido

### Complexidade Espacial

- Cada nó pode ter até 4 filhos (NW, NE, SW, SE)
- Profundidade máxima teórica limitada apenas por precisão de float
- Objetos que cruzam múltiplos quadrantes podem ser inseridos em múltiplos nós

## Características

### Subdivisão Automática

Quando um nó excede a capacidade, ele automaticamente se subdivide em 4 quadrantes:

- **Northwest (NW)**: Quadrante superior esquerdo
- **Northeast (NE)**: Quadrante superior direito
- **Southwest (SW)**: Quadrante inferior esquerdo
- **Southeast (SE)**: Quadrante inferior direito

```csharp
var tree = new QuadTree<int>(new Bounds(0, 0, 100, 100), capacity: 2);

tree.Insert(1, new Bounds(-20, -20, 5, 5)); // OK, 1 item
tree.Insert(2, new Bounds(20, -20, 5, 5));  // OK, 2 itens
tree.Insert(3, new Bounds(-20, 20, 5, 5));  // Dispara subdivisão!
tree.Insert(4, new Bounds(20, 20, 5, 5));   // Vai para subnós
```

### Objetos em Múltiplos Quadrantes

Objetos que cruzam fronteiras de quadrantes podem ser inseridos em múltiplos nós filhos:

```csharp
var tree = new QuadTree<string>(new Bounds(0, 0, 100, 100), capacity: 1);

// Objeto grande que cruza múltiplos quadrantes
var largeBounds = new Bounds(0, 0, 60, 60);
tree.Insert("LargeObject", largeBounds);
// Pode ser inserido em todos os 4 quadrantes que intersecta
```

### Capacidade Configurável

A capacidade determina quando subdividir:

- **Baixa capacidade (2-4)**: Árvore mais profunda, melhor para muitos objetos pequenos
- **Alta capacidade (8-16)**: Árvore mais rasa, melhor para objetos maiores ou menos objetos

```csharp
// Para jogos com muitos objetos pequenos
var denseBattle = new QuadTree<Enemy>(bounds, capacity: 4);

// Para mapas com objetos grandes esparsos
var worldMap = new QuadTree<Building>(bounds, capacity: 16);
```

## Quando Usar QuadTree

### ✅ Use quando

- Precisa de consultas espaciais rápidas (objetos em área)
- Tem muitos objetos distribuídos em espaço 2D
- Objetos têm posições/tamanhos variáveis
- Detecção de colisão entre muitos objetos
- Culling de renderização (não desenhar objetos fora da tela)
- Sistemas de IA que buscam vizinhos

### ❌ Evite quando

- Poucos objetos (< 50) - overhead não compensa
- Objetos estão em grid regular - use DenseGrid/SparseGrid
- Objetos são extremamente grandes (cobrem maior parte do espaço)
- Necessita ordenação espacial estrita (considere KD-Tree)

## Thread Safety

⚠️ **QuadTree não é thread-safe por padrão.** Use locks externos para acesso concorrente:

```csharp
var tree = new QuadTree<GameObject>(bounds);
var lockObj = new object();

// Thread seguro
lock (lockObj)
{
    tree.Insert(obj, objBounds);
}

lock (lockObj)
{
    var results = tree.Query(area);
}
```

## Debug

Use `DebugPrint()` para visualizar estrutura da árvore:

```csharp
var tree = new QuadTree<int>(new Bounds(0, 0, 100, 100), 2);
tree.Insert(1, new Bounds(-20, -20, 5, 5));
tree.Insert(2, new Bounds(20, -20, 5, 5));
tree.Insert(3, new Bounds(-20, 20, 5, 5));

tree.DebugPrint();
// Output:
// QuadTree at (0, 0) [100x100] - 0 items
//   QuadTree at (-25, -25) [50x50] - 1 items
//   QuadTree at (25, -25) [50x50] - 1 items
//   QuadTree at (-25, 25) [50x50] - 1 items
//   QuadTree at (25, 25) [50x50] - 0 items
```

## Integração com Outros Módulos

### Grids

QuadTree é complementar aos Grids - use Grids para estruturas regulares e QuadTree para consultas espaciais dinâmicas:

```csharp
using LSUtils.Grids;
using LSUtils.Spatial;

// Grid para terreno estático
var terrain = new DenseGrid<TerrainType>(100, 100);

// QuadTree para entidades dinâmicas
var entities = new QuadTree<Entity>(new Bounds(50, 50, 100, 100));
```

## Extensões Futuras

Estruturas planejadas para este módulo:

- **Octree**: Particionamento 3D (8 octantes)
- **KD-Tree**: Árvore k-dimensional para buscas de vizinhos mais próximos
- **R-Tree**: Para indexação de geometrias mais complexas

## Ver Também

- [Grids](../Grids/README.md) - Para estruturas regulares de células
- [Testes](../../tests/Spatial/) - Exemplos de uso nos testes unitários
