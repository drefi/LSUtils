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

O terreno padrao do mundo e somente um fallback para pontos sem patch. Ele nao precisa ser modelado como um patch fisico que ocupa todo o mapa.

## Atualizacao de forma

Depois de chamar `SetShape` em um patch ou conteudo ja registrado, chame `TerrainWorld.UpdatePatch` ou `TerrainWorld.UpdateContent`. Isso atualiza o indice espacial para a nova area.

Veja `docs/guides/terrain-world-guide.md` para um fluxo completo e `docs/examples/terrain-world-examples.md` para exemplos de codigo.

## Navegacao

`TerrainWorld.FindPath` usa um perfil de navegacao por agente. O perfil decide o custo de cada patch, quais conteudos bloqueiam passagem e o raio do agente. A implementacao trabalha com `Polygon2D`, cria clearance no espaco de configuracao e nao usa grid.

Para varias consultas no mesmo mundo, use `TerrainWorld.BakeNavigationMesh(settings)` e mantenha a instancia retornada. `BuildNavigationMesh` continua disponivel como alias de compatibilidade.

O bake estatico inclui os limites do mundo, vertices de patches passaveis que representam fronteiras de custo, patches intransitaveis e `TerrainContent` com mobilidade `Static`. Ele fica obsoleto (`IsCurrent == false`) somente quando essa camada estatica muda.

Conteudos criados com `TerrainContentMobility.Dynamic` nao entram no bake. Durante `FindPath`, eles bloqueiam arestas estaticas afetadas e recebem pontos temporarios de clearance. Assim, mover agentes ou outros obstaculos dinamicos exige apenas `TerrainWorld.UpdateContent`, sem refazer a triangulacao estatica.

`TerrainNavigationMesh.BuildStatistics` registra quantos nos, conexoes candidatas, candidatos espaciais e amostras de custo foram processados na reconstrucao. Use esses dados para diagnosticar mapas grandes. O bake primeiro divide as fronteiras nos pontos de intersecao e gera uma triangulacao restrita. Arestas de custo e obstaculos sao recuperadas como arestas obrigatorias, portanto nao podem ser atravessadas por um triangulo.

Depois da triangulacao, a malha verifica a conectividade resultante e pode adicionar pontes locais que tenham passagem valida entre componentes. Isso trata degeneracoes numericas ou triangulos descartados ao redor de obstaculos sem transformar novamente a construcao em um grafo denso.

A topologia tambem recebe um numero limitado de conexoes visiveis entre vizinhos proximos. Essas arestas oferecem alternativas locais ao A* e reduzem desvios causados pela triangulacao, mantendo o crescimento muito abaixo do grafo de visibilidade completo.

Os triangulos validos sao preservados como celulas navegaveis. Cada `TerrainNavigationTriangle` expoe seu `Cost`, e `GetTrianglePatch(index)` retorna o patch dominante usado no bake. Para consultas sem obstaculos dinamicos, o pathfinder executa A* entre triangulos adjacentes usando o custo das duas faces e aplica Funnel sobre os portais compartilhados. O caminho pode atravessar qualquer ponto de um portal e nao fica restrito aos vertices da triangulacao. A topologia de vertices permanece como fallback para camadas dinamicas e degeneracoes geometricas.

Patches passaveis podem ser concavos e sobrepostos; suas intersecoes sao segmentadas durante a subdivisao. Obstaculos bloqueadores ainda precisam ser convexos para que o gerador de clearance por vertices seja correto.
