# Terrain World Guide

O modulo Terrain modela um mundo 2D sem depender de grid. Ele separa quatro preocupacoes: forma geometrica, terreno, conteudo e agrupamento semantico.

## Conceitos

`TerrainPatch<TTerrainType>` e uma area de terreno. Seu tipo pode ser um enum, um identificador ou um objeto de dominio. A forma pode mudar em tempo de execucao, permitindo representar uma poca que cresce, uma area queimada ou uma fronteira redesenhada.

`TerrainContent<TContentType>` usa a mesma ideia para entidades espaciais. Uma arvore, uma rocha ou uma estrutura tem uma forma e pode estar em uma ou mais regioes sem exigir uma interface especifica de conteudo no projeto consumidor.

`TerrainRegion<TTerrainType, TContentType>` e um agrupamento semantico. Ela pode conter patches, conteudos e outras regioes. Os elementos nao possuem um dono exclusivo: compartilhar um patch entre uma regiao de mundo e uma regiao de bioma e um caso de uso normal.

`TerrainWorld<TTerrainType, TContentType>` mantem os indices espaciais e e o ponto de entrada para consultas. O valor padrao do mundo e usado somente quando nenhum patch cobre uma posicao.

## Criando um mundo

```csharp
using LSUtils.Geometry;
using LSUtils.Graphs.Algorithms;
using LSUtils.Spatial;
using LSUtils.Terrain;
using LSUtils.Terrain.Rules;

enum TerrainType { Void, Sand, Water, Grass }
enum ContentType { Tree, Rock, House }

var world = new TerrainWorld<TerrainType, ContentType>(
    new Bounds(0, 0, 100, 100),
    TerrainType.Void);
var sand = new TerrainPatch<TerrainType>(TerrainType.Sand, Square(0, 0, 20));
var water = new TerrainPatch<TerrainType>(TerrainType.Water, Square(6, 6, 4), layer: 1);

world.AddPatch(sand);
world.AddPatch(water);

TerrainType atLake = world.ResolveTerrainTypeAt(7, 7); // Water
TerrainType atBeach = world.ResolveTerrainTypeAt(2, 2); // Sand
```

`Square` e uma funcao auxiliar que cria um `Polygon2D`; uma aplicacao pode fornecer qualquer implementacao de `IShape2D`.

## Sobreposicoes e ordem

Patches sobrepostos sao intencionais. O mundo resolve uma posicao usando, nesta ordem: maior `Layer`, maior `Priority` em caso de empate e o fallback configurado quando nenhum patch contem o ponto. Isso permite agua sobre areia, vegetacao sobre terra e efeitos temporarios acima de ambos.

## Alterando uma area em tempo de execucao

```csharp
water.SetShape(Square(4, 4, 8));
world.UpdatePatch(water);
```

O segundo passo e obrigatorio quando o patch ja esta no mundo: o indice espacial precisa receber os novos bounds. A mesma regra vale para `TerrainContent`.

## Regioes e biomas

```csharp
var worldRegion = new TerrainRegion<TerrainType, ContentType>();
worldRegion.AddPatch(sand);
worldRegion.AddPatch(water);

var coastRegion = new TerrainRegion<TerrainType, ContentType>();
coastRegion.AddPatch(sand);
worldRegion.AddChild(coastRegion);
```

`worldRegion` e `coastRegion` compartilham `sand`. A regiao filha nao agrega area ao pai automaticamente; use a hierarquia para organizacao e regras de dominio, deixando claro o criterio de cada calculo.

Para classificar uma regiao, implemente `ITerrainRegionRule<TBiomeType, TTerrainType, TContentType>`. O avaliador seleciona a regra compativel de maior prioridade e retorna seu resultado.

## Relacoes entre patches

```csharp
var relations = TerrainRelationGraphBuilder.Build(world);
var groups = GraphAlgorithms.ConnectedComponents(relations);
```

O grafo usa `TerrainRelationType`: `Adjacent`, `Overlapping`, `Contains` e `ContainedBy`. Ele e util para grupos conectados, navegacao de alto nivel e regras de fronteira.

## Limite geometrico atual

A descoberta de relacoes usa bounds como classificacao inicial. Ela e rapida e apropriada para candidatos, mas ainda nao executa intersecao exata de dois poligonos arbitrarios. Para regras que dependam de contorno preciso, adicione uma estrategia de relacao exata antes de tomar decisoes de simulacao irreversiveis.

## Thread safety

Os tipos do modulo nao devem ser alterados concorrentemente sem sincronizacao externa.
