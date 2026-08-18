# Terrain

Modulo generico para mundos 2D formados por terrenos de formato e tamanho arbitrarios. Um terreno e uma area (`IShape2D`), nao uma celula de grid.

## Modelo

- `TerrainPatch<TTerrainType>` representa uma area de terreno individual.
- `TerrainContent<TContentType>` representa conteudo espacial, como arvores, pedras ou estruturas.
- `TerrainRegion<TTerrainType, TContentType>` agrupa patches, conteudos e outras regioes.
- `TerrainWorld<TTerrainType, TContentType>` indexa patches e conteudos para consultas espaciais rapidas.
- `TerrainRelationGraphBuilder` cria um grafo de adjacencia, sobreposicao e contencao entre patches.
- `Rules` avalia uma regiao e produz uma classificacao, como um bioma.

## Decisoes de modelagem

Patches podem se sobrepor. `Layer` e `Priority` determinam qual patch vence em uma consulta pontual: maior layer vence; em empate, maior priority vence.

Regioes sao agrupamentos por referencia e nao sao donas de seus patches. Assim, um mesmo patch pode participar de mais de uma regiao. Regioes tambem podem ter filhas; uma regiao de mundo pode, por exemplo, conter regioes de bioma.

Uma regiao possui apenas um pai. `AddChild` transfere a filha do pai anterior e rejeita ciclos diretos ou indiretos. Alteracoes nas formas dos patches e conteudos associados atualizam os bounds da regiao automaticamente.

`TerrainRegion.Area` e `MembershipArea` somam as areas dos patches membros, portanto contam sobreposicoes uma vez por patch. `PolygonCoverageArea` calcula a uniao geometrica e conta cada ponto coberto apenas uma vez; essa operacao requer que todos os patches usem `IPolygonalShape2D` e desconta seus aneis internos.

O terreno padrao do mundo e somente um fallback para pontos sem patch. Ele nao precisa ser modelado como um patch fisico que ocupa todo o mapa.

## Atualizacao de forma

Os setters de `TerrainPatch` e `TerrainContent` notificam os mundos e regioes que registraram o objeto. Alterar forma, tipo, layer, prioridade ou mobilidade atualiza automaticamente o indice espacial e as versoes de navegacao correspondentes.

`TerrainWorld.UpdatePatch` e `TerrainWorld.UpdateContent` continuam disponiveis para compatibilidade. Depois de uma notificacao automatica, essas chamadas sao idempotentes e nao incrementam a versao novamente.

Veja `docs/guides/terrain-world-guide.md` para um fluxo completo e `docs/examples/terrain-world-examples.md` para exemplos de codigo.

## Navegacao

`TerrainWorld.FindPath` usa um perfil de navegacao por agente. O perfil decide o custo de cada patch, quais conteudos bloqueiam passagem e o raio do agente. Patches passaveis aceitam `IPolygonalShape2D`: dentro de um buraco, a resolucao volta ao patch inferior ou ao terreno padrao. Se esse terreno exposto for intransitavel, o anel interno tambem recebe clearance. A implementacao nao usa grid.

`TerrainNavigationSettings.ClearanceArcSegments` controla quantos segmentos aproximam cada canto do clearance. O padrao 3 preserva uma margem conservadora e evita multiplicar nos em mapas com muitas paredes retangulares.

Para varias consultas no mesmo mundo, use `TerrainWorld.BakeNavigationMesh(settings)` e mantenha a instancia retornada. `BuildNavigationMesh` continua disponivel como alias de compatibilidade.

O bake estatico inclui os limites do mundo, vertices de patches passaveis que representam fronteiras de custo, patches intransitaveis e `TerrainContent` com mobilidade `Static`. Ele fica obsoleto (`IsCurrent == false`) somente quando essa camada estatica muda.

Conteudos criados com `TerrainContentMobility.Dynamic` nao entram no bake. Durante `FindPath`, eles bloqueiam arestas estaticas afetadas e recebem pontos temporarios de clearance. Alterar sua forma com `SetShape` atualiza a camada dinamica sem refazer a triangulacao estatica.

`TerrainNavigationMesh.BuildStatistics` registra quantos nos, conexoes candidatas, candidatos espaciais e amostras de custo foram processados na reconstrucao. Use esses dados para diagnosticar mapas grandes. O bake primeiro divide as fronteiras nos pontos de intersecao e gera uma triangulacao restrita. Arestas de custo e obstaculos sao recuperadas como arestas obrigatorias, portanto nao podem ser atravessadas por um triangulo.

Depois da triangulacao, a malha verifica a conectividade resultante e pode adicionar pontes locais que tenham passagem valida entre componentes. Isso trata degeneracoes numericas ou triangulos descartados ao redor de obstaculos sem transformar novamente a construcao em um grafo denso.

A topologia tambem recebe um numero limitado de conexoes visiveis entre vizinhos proximos. Essas arestas oferecem alternativas locais ao A* e reduzem desvios causados pela triangulacao, mantendo o crescimento muito abaixo do grafo de visibilidade completo.

Os triangulos validos sao preservados como celulas navegaveis e para diagnostico. Cada `TerrainNavigationTriangle` expoe seu `Cost`, e `GetTrianglePatch(index)` retorna o patch dominante usado no bake. A rota usa A* sobre o grafo geometrico de arestas visiveis; os custos das arestas estaticas sao calculados no bake e reutilizados entre consultas. Origem e destino recebem apenas um conjunto limitado de conexoes visiveis, evitando reconstruir um grafo denso por agente.

Patches passaveis podem ser concavos, sobrepostos e possuir buracos; suas intersecoes sao segmentadas durante a subdivisao. Aneis internos que exponham terreno intransitavel e outros obstaculos bloqueadores ainda precisam ser convexos para que o gerador de clearance por vertices seja correto.

`TerrainContent` continua aceitando qualquer `IShape2D`, portanto `PolygonArea2D` ja pode representar selecao, area e renderizacao de footprints estruturais. Para participar como obstaculo de navegacao, um conteudo ainda precisa de `Polygon2D` convexo; decomposicao estrutural e clearance de patios internos pertencem a uma camada posterior de estruturas.
