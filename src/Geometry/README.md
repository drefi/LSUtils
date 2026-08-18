# Geometry

Tipos geometricos 2D usados por modulos que precisam modelar areas, limites e relacoes espaciais.

## Tipos principais

- `IShape2D`: contrato para uma forma com `Bounds`, `Area` e `Contains`.
- `Polygon2D`: poligono simples definido por vertices `LSVector2`.
- `IPolygonalShape2D`: contrato comum para areas delimitadas por aneis.
- `PolygonArea2D`: contorno externo com zero ou mais aneis internos estritamente contidos.
- `PointLocation`: distingue pontos no interior, exterior ou exatamente na borda.
- `ShapeRelation`: resultado de uma comparacao espacial.
- `GeometryRelations`: classificacao inicial entre duas formas.
- `ConstrainedTriangulation2D`: triangulacao de segmentos com fronteiras obrigatorias.
- `PolygonTriangulation2D`: triangulacao ja filtrada para uma `IPolygonalShape2D`, incluindo buracos.

## Triangulacao restrita

`ConstrainedTriangulation2D.Triangulate` recebe segmentos independentes de qualquer dominio. O algoritmo divide cruzamentos e sobreposicoes colineares em vertices, gera uma triangulacao de Delaunay inicial e recupera cada segmento obrigatorio por troca de arestas ou retriangulacao local da cavidade atravessada. O resultado expoe vertices, triangulos e as constraints ja divididas.

O triangulador nao classifica interior, exterior, custo ou obstaculos. Modulos consumidores, como `Terrain.Navigation`, fazem essa classificacao sobre os triangulos resultantes.

## Exemplo

```csharp
var lake = new Polygon2D(new[] {
    new LSVector2(0, 0), new LSVector2(8, 0),
    new LSVector2(8, 6), new LSVector2(0, 6)
});

bool isInsideLake = lake.Contains(4, 3); // true
float area = lake.Area; // 48
```

Uma area com buraco preserva os aneis como geometria explicita:

```csharp
var island = new PolygonArea2D(lake, new[] {
    new Polygon2D(new[] {
        new LSVector2(2, 2), new LSVector2(2, 4),
        new LSVector2(6, 4), new LSVector2(6, 2)
    })
});

bool isLand = island.Contains(1, 1); // true
bool isHole = island.Contains(4, 3); // false
var triangles = PolygonTriangulation2D.Triangulate(island);
```

`PolygonArea2D` normaliza o contorno externo para anti-horario e os buracos para horario. Aneis autointersectantes, externos, sobrepostos, aninhados ou tangentes sao rejeitados.

## Precisao atual

`GeometryRelations.Classify` usa os `Bounds` das formas como fase de classificacao. Isto e adequado para descoberta rapida de candidatos espaciais, mas nao substitui uma intersecao exata poligono-poligono. Formas com bounds que se cruzam podem ser classificadas como `Intersects` mesmo quando a geometria precisa de um teste mais preciso.

Essa escolha deixa a API pronta para uma futura estrategia de geometria exata sem acoplar o modulo Terrain a uma representacao especifica de poligono.
