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
