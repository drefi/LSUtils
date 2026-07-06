namespace LSUtils.Spatial;

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
            throw new LSArgumentException("Cell size must be greater than 0", nameof(cellSize));
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

    public bool HasCollision(Bounds bounds) {
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
    public bool HasCollision(T item, Bounds bounds) {
        // Pegamos onde o item estava antes do movimento
        bool tinhaBoundsAntigo = _itemBounds.TryGetValue(item, out Bounds oldBounds);

        var seen = new HashSet<T>();
        foreach (var cell in getOverlappingCells(bounds)) {
            if (!_cells.TryGetValue(cell, out var itemsInCell))
                continue;

            foreach (var otherItem in itemsInCell) {
                if (otherItem.Equals(item)) continue; // Ignora a si mesmo

                if (seen.Add(otherItem)) {
                    // Se já colidiam ANTES, ignoramos para permitir que se separem
                    if (tinhaBoundsAntigo && _itemBounds[otherItem].Intersects(oldBounds)) {
                        continue;
                    }

                    if (_itemBounds[otherItem].Intersects(bounds))
                        return true;
                }
            }
        }
        return false;
    }
    public bool HasCollision(T item, Bounds oldBounds, Bounds newBounds) {
        var seen = new HashSet<T>();

        foreach (var cell in getOverlappingCells(newBounds)) {
            if (!_cells.TryGetValue(cell, out var itemsInCell))
                continue;

            foreach (var otherItem in itemsInCell) {
                if (otherItem.Equals(item))
                    continue; // Ignora a si mesmo (posição antiga nas células)

                if (seen.Add(otherItem)) {
                    // Se eles JÁ estavam colidindo na posição antiga, 
                    // ignoramos para permitir que eles se separem naturalmente.
                    if (_itemBounds[otherItem].Intersects(oldBounds)) {
                        continue;
                    }

                    if (_itemBounds[otherItem].Intersects(newBounds))
                        return true; // Colisão real detectada
                }
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
        if (currentBounds == newBounds) return true; //mesmo bounds retorna true
        if (!allowOverlap) {
            // Temporarily remove to avoid self-collision
            //removeFromCells(item, currentBounds);
            // Passamos o 'item' para avaliar colisões permitidas (separação)
            if (HasCollision(item, newBounds)) {
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
    /// Move um objeto para uma nova posição se for válido, ou força o movimento se allowOverlap for true.
    /// </summary>
    /// <returns>True se o objeto se moveu com sucesso, false se colidiu e não se moveu.</returns>
    public bool Move(T item, Bounds newBounds, bool allowOverlap = false) {
        // 1. Se o item não existe, não faz nada
        if (!_itemBounds.TryGetValue(item, out Bounds currentBounds))
            return false;

        // 2. Se a posição é idêntica, não gasta processamento
        if (currentBounds == newBounds)
            return true;

        // 3. Se não permite sobreposição, testamos o futuro ANTES de mexer nas células
        if (!allowOverlap) {
            // Passamos 'item' e 'currentBounds' para o HasCollision ignorar colisões antigas/consigo mesmo
            if (HasCollision(item, currentBounds, newBounds)) {
                return false; // Colidiu! Cancela o movimento sem ter alterado nada na grade.
            }
        }

        // 4. Se passou no teste (ou allowOverlap é true), atualizamos a grade de forma eficiente
        // Otimização: Só mexemos nas células que realmente mudaram!
        updateCellsForMove(item, currentBounds, newBounds);

        _itemBounds[item] = newBounds;
        return true;
    }
    public bool Move(T item, Bounds newBounds, bool allowOverlap, out T? collidedWith) {
        collidedWith = default;

        if (!_itemBounds.TryGetValue(item, out Bounds currentBounds))
            return false;

        if (currentBounds == newBounds)
            return true;

        if (!allowOverlap) {
            // Agora o HasCollision devolve o item que causou o bloqueio
            if (HasCollision(item, currentBounds, newBounds, out collidedWith)) {
                return false;
            }
        }

        updateCellsForMove(item, currentBounds, newBounds);
        _itemBounds[item] = newBounds;
        return true;
    }

    private bool HasCollision(T item, Bounds oldBounds, Bounds newBounds, out T? collidedWith) {
        collidedWith = default;
        var seen = new HashSet<T>();

        foreach (var cell in getOverlappingCells(newBounds)) {
            if (!_cells.TryGetValue(cell, out var itemsInCell))
                continue;

            foreach (var otherItem in itemsInCell) {
                if (otherItem.Equals(item)) continue;

                if (seen.Add(otherItem)) {
                    // Se JÁ estavam colidindo antes, ignoramos para o movimento NÃO travar,
                    // mas o afastamento natural vai acontecer via física no loop principal.
                    if (_itemBounds[otherItem].Intersects(oldBounds)) {
                        continue;
                    }

                    if (_itemBounds[otherItem].Intersects(newBounds)) {
                        collidedWith = otherItem; // Guarda quem causou a colisão
                        return true;
                    }
                }
            }
        }
        return false;
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
    public int GetCellCount(float x, float y) {
        int cellX = toCellCoordinate(x);
        int cellY = toCellCoordinate(y);
        var key = new CellKey(cellX, cellY);

        if (_cells.TryGetValue(key, out var itemsInCell)) {
            return itemsInCell.Count; // O(1)
        }

        return 0;
    }
    private void addToCell(CellKey cell, T item) {
        if (!_cells.TryGetValue(cell, out HashSet<T>? itemsInCell)) {
            itemsInCell = new HashSet<T>();
            _cells[cell] = itemsInCell;
        }

        itemsInCell.Add(item);
    }
    private void updateCellsForMove(T item, Bounds oldBounds, Bounds newBounds) {
        int oldMinX = toCellCoordinate(oldBounds.MinX);
        int oldMaxX = toCellCoordinate(oldBounds.MaxX);
        int oldMinY = toCellCoordinate(oldBounds.MinY);
        int oldMaxY = toCellCoordinate(oldBounds.MaxY);

        int newMinX = toCellCoordinate(newBounds.MinX);
        int newMaxX = toCellCoordinate(newBounds.MaxX);
        int newMinY = toCellCoordinate(newBounds.MinY);
        int newMaxY = toCellCoordinate(newBounds.MaxY);

        // 1. Se continuam exatamente nas mesmas células, sai imediatamente (O(1))
        if (oldMinX == newMinX && oldMaxX == newMaxX && oldMinY == newMinY && oldMaxY == newMaxY) {
            return;
        }

        // 2. OTIMIZAÇÃO DE MEMÓRIA: Se a nova área não intersecta em NADA a antiga,
        // é mais rápido limpar tudo da antiga e colocar tudo na nova do que testar célula por célula.
        bool intersectam = !(newMinX > oldMaxX || newMaxX < oldMinX || newMinY > oldMaxY || newMaxY < oldMinY);

        if (!intersectam) {
            // Remove de todas as antigas diretamente
            for (int x = oldMinX; x <= oldMaxX; x++) {
                for (int y = oldMinY; y <= oldMaxY; y++) {
                    if (_cells.TryGetValue(new CellKey(x, y), out var itemsInCell)) {
                        itemsInCell.Remove(item);
                        // NOTA: Removeu-se o _cells.Remove(key) para evitar alocação/desalocação de memória do dicionário principal.
                    }
                }
            }
            // Adiciona em todas as novas diretamente
            for (int x = newMinX; x <= newMaxX; x++) {
                for (int y = newMinY; y <= newMaxY; y++) {
                    addToCell(new CellKey(x, y), item);
                }
            }
            return;
        }

        // 3. Se elas se intersectam, removemos apenas das bordas que deixaram de existir
        for (int x = oldMinX; x <= oldMaxX; x++) {
            for (int y = oldMinY; y <= oldMaxY; y++) {
                // Se a célula antiga NÃO está na nova área
                if (x < newMinX || x > newMaxX || y < newMinY || y > newMaxY) {
                    if (_cells.TryGetValue(new CellKey(x, y), out var itemsInCell)) {
                        itemsInCell.Remove(item);
                    }
                }
            }
        }

        // Adiciona apenas nas novas bordas que entraram
        for (int x = newMinX; x <= newMaxX; x++) {
            for (int y = newMinY; y <= newMaxY; y++) {
                // Se a célula nova NÃO estava na área antiga
                if (x < oldMinX || x > oldMaxX || y < oldMinY || y > oldMaxY) {
                    addToCell(new CellKey(x, y), item);
                }
            }
        }
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
        return (int)LSMath.Floor(value / _cellSize);
    }

    private readonly record struct CellKey(int X, int Y);
}
