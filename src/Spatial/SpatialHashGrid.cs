namespace LSUtils.Spatial;

using System;
using System.Collections.Generic;
/// <summary>
/// Implementação de grade hash espacial para consultas 2D com atualização frequente.
/// </summary>
/// <typeparam name="T">Tipo dos objetos armazenados.</typeparam>
public class SpatialHashGrid<T> : ISpatialIndex<T> where T : notnull {
    private readonly float _cellSize;
    private readonly Dictionary<CellKey, HashSet<T>> _cells;
    private readonly Dictionary<T, Bounds> _itemBounds;

    /// <summary>
    /// Cria uma nova grade hash espacial.
    /// </summary>
    /// <param name="cellSize">Tamanho de cada célula da grade.</param>
    public SpatialHashGrid(float cellSize) {
        if (cellSize <= 0) {
            throw new ArgumentException("Cell size must be greater than 0", nameof(cellSize));
        }

        _cellSize = cellSize;
        _cells = new Dictionary<CellKey, HashSet<T>>();
        _itemBounds = new Dictionary<T, Bounds>();
    }

    /// <summary>
    /// Tamanho de cada célula da grade.
    /// </summary>
    public float CellSize => _cellSize;

    /// <summary>
    /// Número total de objetos únicos indexados.
    /// </summary>
    public int Count => _itemBounds.Count;

    /// <summary>
    /// Insere um objeto na grade.
    /// </summary>
    /// <param name="item">O objeto a ser inserido.</param>
    /// <param name="bounds">Os limites espaciais do objeto.</param>
    /// <param name="allowOverlap">Indica se sobreposições são permitidas.</param>
    /// <returns>True se inserido com sucesso, false caso contrário.</returns>
    public bool Insert(T item, Bounds bounds, bool allowOverlap = false) {
        if (_itemBounds.ContainsKey(item))
            return false;

        if (!allowOverlap && HasCollision(bounds))
            return false;

        foreach (var cell in getOverlappingCells(bounds))
            addToCell(cell, item);

        _itemBounds[item] = bounds;
        return true;
    }

    private bool HasCollision(Bounds bounds) {
        var seen = new HashSet<T>();
        foreach (var cell in getOverlappingCells(bounds)) {
            if (!_cells.TryGetValue(cell, out var itemsInCell))
                continue;

            foreach (var item in itemsInCell) {
                if (seen.Add(item) && _itemBounds[item].Intersects(bounds))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Consulta objetos dentro de uma área.
    /// </summary>
    public IReadOnlyList<T> Query(Bounds area) {
        var result = new List<T>();
        var seen = new HashSet<T>();

        foreach (var cell in getOverlappingCells(area)) {
            if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
                continue;
            }

            foreach (var item in itemsInCell) {
                if (!seen.Add(item)) {
                    continue;
                }

                if (_itemBounds[item].Intersects(area)) {
                    result.Add(item);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Atualiza os limites de um objeto na grade.
    /// </summary>
    /// <param name="item">O objeto a ser atualizado.</param>
    /// <param name="newBounds">Os novos limites espaciais do objeto.</param>
    /// <returns>True se atualizado com sucesso, false caso contrário.</returns>
    public bool Update(T item, Bounds newBounds, bool allowOverlap = false) {
        if (!_itemBounds.TryGetValue(item, out Bounds currentBounds))
            return false;

        if (!allowOverlap) {
            // Temporarily remove to avoid self-collision
            removeFromCells(item, currentBounds);
            if (HasCollision(newBounds)) {
                // Rollback
                foreach (var cell in getOverlappingCells(currentBounds))
                    addToCell(cell, item);
                return false;
            }
        } else {
            removeFromCells(item, currentBounds);
        }

        foreach (var cell in getOverlappingCells(newBounds))
            addToCell(cell, item);

        _itemBounds[item] = newBounds;
        return true;
    }

    /// <summary>
    /// Remove um objeto da grade.
    /// </summary>
    public bool Remove(T item, out Bounds oldBounds) {
        if (!_itemBounds.TryGetValue(item, out oldBounds)) {
            oldBounds = default;
            return false;
        }

        removeFromCells(item, oldBounds);
        _itemBounds.Remove(item);
        return true;
    }

    /// <summary>
    /// Remove todos os objetos da grade.
    /// </summary>
    public void Clear() {
        _cells.Clear();
        _itemBounds.Clear();
    }

    private void addToCell(CellKey cell, T item) {
        if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
            itemsInCell = new HashSet<T>();
            _cells[cell] = itemsInCell;
        }

        itemsInCell.Add(item);
    }

    private void removeFromCells(T item, Bounds bounds) {
        foreach (var cell in getOverlappingCells(bounds)) {
            if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
                continue;
            }

            itemsInCell.Remove(item);
            if (itemsInCell.Count == 0) {
                _cells.Remove(cell);
            }
        }
    }

    private IEnumerable<CellKey> getOverlappingCells(Bounds bounds) {
        int minX = toCellCoordinate(bounds.MinX);
        int maxX = toCellCoordinate(bounds.MaxX);
        int minY = toCellCoordinate(bounds.MinY);
        int maxY = toCellCoordinate(bounds.MaxY);

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                yield return new CellKey(x, y);
            }
        }
    }

    private int toCellCoordinate(float value) {
        return (int)MathF.Floor(value / _cellSize);
    }

    private readonly record struct CellKey(int X, int Y);
}
