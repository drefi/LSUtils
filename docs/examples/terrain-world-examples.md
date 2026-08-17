# Terrain World Examples

Os exemplos usam enums apenas para deixar o dominio compacto. Tipos de terreno, conteudo e bioma podem ser classes ou identificadores do projeto consumidor.

## Forma auxiliar

```csharp
using LSUtils.Geometry;
using LSUtils.Graphs.Algorithms;
using LSUtils.Spatial;
using LSUtils.Terrain;
using LSUtils.Terrain.Rules;

static Polygon2D Square(float x, float y, float size) => new(new[] {
    new LSVector2(x, y), new LSVector2(x + size, y),
    new LSVector2(x + size, y + size), new LSVector2(x, y + size)
});
```

## Poca expansivel sobre areia

```csharp
enum TerrainType { Void, Sand, Water }
enum ContentType { Tree, Rock }

var world = new TerrainWorld<TerrainType, ContentType>(
    new Bounds(0, 0, 100, 100),
    TerrainType.Void);
var sand = new TerrainPatch<TerrainType>(TerrainType.Sand, Square(0, 0, 30));
var puddle = new TerrainPatch<TerrainType>(TerrainType.Water, Square(10, 10, 2), layer: 1);

world.AddPatch(sand);
world.AddPatch(puddle);

puddle.SetShape(Square(7, 7, 8));

var terrain = world.ResolveTerrainTypeAt(8, 8); // Water
```

`SetShape` notifica o mundo automaticamente. `UpdatePatch` ainda pode aparecer em integracoes antigas, mas e idempotente quando a mudanca ja foi sincronizada.

## Conteudo e regioes aninhadas

```csharp
var tree = new TerrainContent<ContentType>(ContentType.Tree, Square(3, 3, 1));
world.AddContent(tree);

var worldRegion = new TerrainRegion<TerrainType, ContentType>();
worldRegion.AddPatch(sand);
worldRegion.AddPatch(puddle);
worldRegion.AddContent(tree);

var oasis = new TerrainRegion<TerrainType, ContentType>();
oasis.AddPatch(puddle);
oasis.AddContent(tree);
worldRegion.AddChild(oasis);
```

O mesmo patch e conteudo aparecem em ambas as regioes, sem duplicacao. Remover um item de uma regiao nao o remove do mundo nem das demais regioes.

## Bioma por regra

```csharp
enum Biome { Unknown, Beach, Oasis }

sealed class OasisRule : ITerrainRegionRule<Biome, TerrainType, ContentType> {
    public int Priority => 100;
    public Biome Result => Biome.Oasis;

    public bool Matches(TerrainRegionEvaluationContext<TerrainType, ContentType> context) {
        return context.GetPatchAreaRatio(TerrainType.Water) >= 0.5f
            && context.GetContentCount(ContentType.Tree) > 0;
    }
}

Biome biome = TerrainRegionEvaluator.Evaluate(
    oasis,
    new ITerrainRegionRule<Biome, TerrainType, ContentType>[] { new OasisRule() },
    Biome.Unknown);
```

## Grafo de relacoes e componentes

```csharp
var graph = TerrainRelationGraphBuilder.Build(world);
var groups = GraphAlgorithms.ConnectedComponents(graph);
```

`groups` separa conjuntos de patches que possuem relacoes detectadas. Isso pode alimentar regras de ecossistema, navegacao de alto nivel ou verificacao de ilhas desconectadas.
