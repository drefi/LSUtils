namespace LSUtils.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;

public class SpatialHashGrid<T> : ISpatialIndex<T> where T : notnull {
    public readonly record struct CellKey(int X, int Y);
    private readonly float _cellSize;
    private readonly Dictionary<CellKey, HashSet<T>> _cells;
    private readonly Dictionary<T, Bounds> _itemsBounds;

    public float CellSize => _cellSize;
    public int Count => _itemsBounds.Count;
    //public CellKey[] ActiveCells => _cells.Keys.ToArray();

    public SpatialHashGrid(float cellSize) {
        if (cellSize <= 0) {
            throw new LSArgumentException("Cell size must be greater than 0", nameof(cellSize));
        }

        _cellSize = cellSize;
        _cells = new();
        _itemsBounds = new();
    }

    public int ToCellCoordinate(float value) {
        return (int)MathF.Floor(value / CellSize);
    }

    /// <summary>
    /// Insere ou atualiza um objeto na grade com alocação zero de memória.
    /// </summary>
    public bool InsertOrUpdate(T item, Bounds bounds) {
        bool itemExiste = _itemsBounds.TryGetValue(item, out var oldBounds);

        if (itemExiste) {
            if (oldBounds == bounds) return true; // Mesmo local, não faz nada

            // Remove cirurgicamente das células antigas usando loops numéricos planos (Sem gerar lixo)
            int oldMinX = ToCellCoordinate(oldBounds.MinX);
            int oldMaxX = ToCellCoordinate(oldBounds.MaxX);
            int oldMinY = ToCellCoordinate(oldBounds.MinY);
            int oldMaxY = ToCellCoordinate(oldBounds.MaxY);

            for (int x = oldMinX; x <= oldMaxX; x++) {
                for (int y = oldMinY; y <= oldMaxY; y++) {
                    if (_cells.TryGetValue(new CellKey(x, y), out var itemsInCell)) {
                        itemsInCell.Remove(item);
                        // Mantemos o HashSet na memória (_cells.Remove removido de propósito para performance)
                    }
                }
            }
        }

        // Insere nas novas células calculando as coordenadas em tempo de execução
        int newMinX = ToCellCoordinate(bounds.MinX);
        int newMaxX = ToCellCoordinate(bounds.MaxX);
        int newMinY = ToCellCoordinate(bounds.MinY);
        int newMaxY = ToCellCoordinate(bounds.MaxY);

        for (int x = newMinX; x <= newMaxX; x++) {
            for (int y = newMinY; y <= newMaxY; y++) {
                var key = new CellKey(x, y);
                if (!_cells.TryGetValue(key, out var itemsInCell)) {
                    itemsInCell = new HashSet<T>();
                    _cells[key] = itemsInCell;
                }
                itemsInCell.Add(item);
            }
        }

        _itemsBounds[item] = bounds;
        return true;
    }

    /// <summary>
    /// Consulta objetos de forma altamente otimizada compartilhando o HashSet de controle.
    /// </summary>
    public void Query(Bounds area, ICollection<T> result, HashSet<T>? reuseSeenSet = null) {
        HashSet<T> seen = reuseSeenSet ?? new HashSet<T>();
        if (reuseSeenSet != null) seen.Clear();

        int minX = ToCellCoordinate(area.MinX);
        int maxX = ToCellCoordinate(area.MaxX);
        int minY = ToCellCoordinate(area.MinY);
        int maxY = ToCellCoordinate(area.MaxY);

        for (int x = minX; x <= maxX; x++) {
            // Calcula os limites matemáticos desta célula específica em tempo de execução
            float cellMinX = x * CellSize;
            float cellMaxX = cellMinX + CellSize;

            // Se a busca cobre totalmente o eixo X desta célula
            bool xTotalmenteContido = area.MinX <= cellMinX && area.MaxX >= cellMaxX;

            for (int y = minY; y <= maxY; y++) {
                if (!_cells.TryGetValue(new CellKey(x, y), out var itemsInCell) || itemsInCell.Count == 0)
                    continue;

                float cellMinY = y * CellSize;
                float cellMaxY = cellMinY + CellSize;

                // Se a busca cobre totalmente o eixo Y desta célula
                bool yTotalmenteContido = area.MinY <= cellMinY && area.MaxY >= cellMaxY;

                // Se a célula inteira está dentro da área de Query, ignoramos o teste de Intersects individual!
                bool celulaTotalmenteContida = xTotalmenteContido && yTotalmenteContido;

                foreach (var item in itemsInCell) {
                    // 1. Primeiro checa se já viu (O(1)). Se já viu, ignora o resto.
                    if (!seen.Add(item))
                        continue;

                    // 2. Se a célula está 100% contida na busca, adiciona direto sem testar o Intersects (Salva milhões de cálculos)
                    if (celulaTotalmenteContida) {
                        result.Add(item);
                        continue;
                    }

                    // 3. Fallback apenas para as células das bordas da área de Query
                    if (_itemsBounds[item].Intersects(area)) {
                        result.Add(item);
                    }
                }
            }
        }
    }

    public bool TryGetBounds(T item, out Bounds bounds) {
        return _itemsBounds.TryGetValue(item, out bounds);
    }
    public Bounds GetBounds(T item) {
        return TryGetBounds(item, out var bounds) ? bounds : throw new LSException($"{item} not found.");
    }


    public bool Remove(T item) {
        if (!_itemsBounds.TryGetValue(item, out var oldBounds)) {
            return false;
        }

        int minX = ToCellCoordinate(oldBounds.MinX);
        int maxX = ToCellCoordinate(oldBounds.MaxX);
        int minY = ToCellCoordinate(oldBounds.MinY);
        int maxY = ToCellCoordinate(oldBounds.MaxY);

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                if (_cells.TryGetValue(new CellKey(x, y), out var itemsInCell)) {
                    itemsInCell.Remove(item);
                }
            }
        }

        _itemsBounds.Remove(item);
        return true;
    }

    public void Clear() {
        _cells.Clear();
        _itemsBounds.Clear();
    }
    // HashSet<T>? _itemsCellTmp = new();
    // public HashSet<T> GetItemsInCell(CellKey cellKey) {
    //     if (!_cells.TryGetValue(cellKey, out _itemsCellTmp)) {
    //         _itemsCellTmp = new();
    //     }
    //     return _itemsCellTmp;
    // }
    private readonly HashSet<T> _emptySet = new();

    // 2. Altere o ActiveCells para filtrar APENAS quem realmente tem itens dentro
    public IEnumerable<CellKey> ActiveCells => _cells.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key);

    // Se preferir manter como array para indexação rápida, faça com filtragem eficiente:
    public CellKey[] GetActiveCells() {
        // Lista auxiliar reutilizável na classe para evitar o ToArray() bruto do LINQ se quiser, 
        // mas a filtragem abaixo já mata o problema das células fantasma:
        return _cells.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).ToArray();
    }

    // 3. Corrija o GetItemsInCell removendo a variável global mutável perigosa
    public HashSet<T> GetItemsInCell(CellKey cellKey) {
        // Retorna o set real se houver, ou o set vazio compartilhado estável
        return _cells.TryGetValue(cellKey, out var itemsInCell) ? itemsInCell : _emptySet;
    }
}
