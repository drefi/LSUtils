namespace LSUtils.Spatial;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Implementação de QuadTree para particionamento espacial hierárquico 2D.
/// Divide recursivamente o espaço em quadrantes para consultas espaciais eficientes.
/// </summary>
/// <typeparam name="T">Tipo dos objetos armazenados.</typeparam>
public class QuadTree<T> : ISpatialIndex<T> where T : notnull {
    private (QuadTree<T> NW, QuadTree<T> NE, QuadTree<T> SW, QuadTree<T> SE)? _quadrants = null;
    public readonly record struct QuadTreeEntry(QuadTree<T> Node, Bounds Bounds);
    private (Bounds NW, Bounds NE, Bounds SW, Bounds SE) _quadrantBounds;

    private readonly Bounds _bounds;
    private readonly int _capacity;
    //private int _count;
    private readonly Dictionary<T, QuadTreeEntry> _quadTreeEntries = new();
    private readonly HashSet<T> _items = new();
    private readonly QuadTree<T>? _parent;

    /// <summary>
    /// Número total de objetos na árvore.
    /// </summary>
    public int Count => _quadTreeEntries.Count;

    /// <summary>
    /// Limites espaciais desta árvore.
    /// </summary>
    public Bounds Bounds => _bounds;

    /// <summary>
    /// Capacidade máxima de objetos antes de subdividir.
    /// </summary>
    public int Capacity => _capacity;
    public int Depth { get; }

    protected QuadTree(QuadTree<T> parent, Bounds bounds, int depth, Dictionary<T, QuadTreeEntry> entries) {
        _parent = parent;
        _bounds = bounds;
        _capacity = parent.Capacity;

        _quadrants = null;
        _quadTreeEntries = entries;
        var h = _bounds.Height / 2;
        var hh = h / 2;
        var w = _bounds.Width / 2;
        var hw = w / 2;
        _quadrantBounds = (
            // NW (cima-esquerda)
            new Bounds(_bounds.X - hw, _bounds.Y + hh, w, h),
            // NE (cima-direita)
            new Bounds(_bounds.X + hw, _bounds.Y + hh, w, h),
            // SW (baixo-esquerda)
            new Bounds(_bounds.X - hw, _bounds.Y - hh, w, h),
            // SE (baixo-direita)
            new Bounds(_bounds.X + hw, _bounds.Y - hh, w, h)
    );
        Depth = depth;
    }

    /// <summary>
    /// Cria uma nova QuadTree.
    /// </summary>
    /// <param name="bounds">Limites espaciais da árvore.</param>
    /// <param name="capacity">Número máximo de objetos por nó antes de subdividir (padrão: 64).</param>
    public QuadTree(int depth, Bounds bounds, int capacity = 64) {
        if (capacity <= 0)
            throw new LSArgumentException("Capacity must be greater than 0", nameof(capacity));
        _parent = null;
        _bounds = bounds;
        _capacity = capacity;
        _quadTreeEntries = new();
        _quadrants = null;
        Depth = depth;
    }

    private void clearNode() {
        if (_quadrants.HasValue) {
            _quadrants?.NW.clearNode();
            _quadrants?.NE.clearNode();
            _quadrants?.SW.clearNode();
            _quadrants?.SE.clearNode();
            _quadrants = null;
        }
        _items.Clear();
    }

    /// <summary>
    /// Checks if bounds can fit into any of the child quadrants.
    /// </summary>
    private bool CanFitInQuadrants(Bounds bounds) {
        return _quadrantBounds.NW.Contains(bounds) ||
               _quadrantBounds.NE.Contains(bounds) ||
               _quadrantBounds.SW.Contains(bounds) ||
               _quadrantBounds.SE.Contains(bounds);
    }

    /// <summary>
    /// Tenta subdividir o nó atual.
    /// Retorna true se a subdivisão ocorreu.
    /// </summary>
    private bool trySubdivide(Bounds newItemBounds) {
        // Já subdividido ou sem profundidade restante
        if (_quadrants.HasValue || Depth <= 0)
            return false;

        // Ainda há espaço neste nó
        if (_items.Count < _capacity)
            return false;

        // Só subdivide se pelo menos um item (existente ou o novo) puder caber em algum filho
        bool anyCanFit = CanFitInQuadrants(newItemBounds);

        if (!anyCanFit) {
            foreach (var item in _items) {
                if (_quadTreeEntries.TryGetValue(item, out var entry) &&
                    CanFitInQuadrants(entry.Bounds)) {
                    anyCanFit = true;
                    break;
                }
            }
        }

        if (!anyCanFit)
            return false;

        _quadrants = (
            new QuadTree<T>(this, _quadrantBounds.NW, Depth - 1, _quadTreeEntries),
            new QuadTree<T>(this, _quadrantBounds.NE, Depth - 1, _quadTreeEntries),
            new QuadTree<T>(this, _quadrantBounds.SW, Depth - 1, _quadTreeEntries),
            new QuadTree<T>(this, _quadrantBounds.SE, Depth - 1, _quadTreeEntries)
        );

        // Redistribui os itens que cabem nos filhos
        var toRemove = new List<T>();

        foreach (var item in _items) {
            if (!_quadTreeEntries.TryGetValue(item, out var entry))
                throw new LSException($"{item} entry not found during subdivide.");

            var child = GetQuadrant(entry.Bounds);
            if (child == null)
                continue; // continua neste nó

            // Inserção direta no filho (ele ainda é folha)
            child._items.Add(item);
            _quadTreeEntries[item] = new QuadTreeEntry(child, entry.Bounds);
            toRemove.Add(item);
        }

        foreach (var item in toRemove)
            _items.Remove(item);

        return true;
    }
    /// <summary>
    /// Insere um objeto na árvore.
    /// </summary>
    /// <param name="item">O objeto a ser inserido.</param>
    /// <param name="bounds">Os limites espaciais do objeto.</param>
    /// <returns>True se inserido com sucesso, false caso contrário.</returns>
    public bool Insert(T item, Bounds bounds) {
        // 1. Rejeição rápida
        if (!_bounds.Intersects(bounds))
            return false;

        // 2. Já existe? (evita bagunça no count e na entry)
        if (_quadTreeEntries.ContainsKey(item))
            return false; // ou Update interno, se preferir

        // 3. Tenta descer o mais fundo possível
        if (_quadrants.HasValue) {
            var child = GetQuadrant(bounds);
            if (child != null && child.Insert(item, bounds))
                return true;
            // se não coube em nenhum filho, cai para este nó
        } else {
            // só tenta subdividir se realmente necessário
            if (_items.Count >= _capacity && Depth > 0) {
                if (trySubdivide(bounds)) {
                    var child = GetQuadrant(bounds);
                    if (child != null && child.Insert(item, bounds))
                        return true;
                }
            }
        }

        // 4. Armazena neste nó
        _items.Add(item);
        _quadTreeEntries[item] = new QuadTreeEntry(this, bounds);
        return true;
    }
    /// <summary>
    /// Atualiza os bounds de um item já presente na árvore.
    /// Tenta manter o item no mesmo nó quando possível.
    /// </summary>
    public bool Update(T item, Bounds newBounds) {
        if (!_quadTreeEntries.TryGetValue(item, out var entry))
            return false;

        var currentNode = entry.Node;

        // Caso rápido: continua cabendo no mesmo nó
        if (currentNode._bounds.Contains(newBounds)) {
            // Ainda é o melhor lugar? (opcional: verificar se agora cabe em um filho)
            if (currentNode._quadrants.HasValue) {
                var betterChild = currentNode.GetQuadrant(newBounds);
                if (betterChild != null) {
                    // Move para o filho
                    currentNode._items.Remove(item);
                    betterChild.Insert(item, newBounds); // Insert já atualiza a entry
                    return true;
                }
            }

            // Só atualiza os bounds
            _quadTreeEntries[item] = new QuadTreeEntry(currentNode, newBounds);
            return true;
        }

        // Não cabe mais no nó atual → precisa subir e reinserir
        // Remove do nó atual sem apagar a entry global ainda
        if (!currentNode._items.Remove(item))
            throw new LSException($"Inconsistent state removing {item}");

        // Sobe até encontrar um ancestral que contenha o novo bounds
        // (ou vai direto no root se preferir simplicidade)
        QuadTree<T> node = currentNode;
        while (node._parent != null && !node._bounds.Contains(newBounds)) {
            node = node._parent;
        }

        // Reinsere a partir desse nó (ou do root)
        // Como a entry ainda existe, o Insert precisa de uma variante "force" ou
        // fazemos a inserção manualmente.

        return node.InsertAfterMove(item, newBounds);
    }

    // Variante interna usada só pelo Update
    private bool InsertAfterMove(T item, Bounds bounds) {
        if (!_bounds.Intersects(bounds))
            return false;

        if (_quadrants.HasValue) {
            var child = GetQuadrant(bounds);
            if (child != null)
                return child.InsertAfterMove(item, bounds);
        } else if (_items.Count >= _capacity && Depth > 0) {
            if (trySubdivide(bounds)) {
                var child = GetQuadrant(bounds);
                if (child != null)
                    return child.InsertAfterMove(item, bounds);
            }
        }

        _items.Add(item);
        _quadTreeEntries[item] = new QuadTreeEntry(this, bounds);
        return true;
    }
    public void Query(Bounds area, ICollection<T> result, T[]? mask = null) {
        HashSet<T> seen = new HashSet<T>();
        if (!_bounds.Intersects(area)) return;

        foreach (var item in _items) {
            if (mask != null && mask.Contains(item)) continue;
            if (_quadTreeEntries.TryGetValue(item, out var quadTreeEntry) == false) throw new LSException($"_items[{_items.Count}]: {item} does not exist in _quadTreeEntries[{_quadTreeEntries.Count}].");
            if (!seen.Add(item)) continue;
            if (!area.Intersects(quadTreeEntry.Bounds)) continue;
            result.Add(item);
        }

        if (_quadrants.HasValue) {
            _quadrants.Value.NW.Query(area, result, mask);
            _quadrants.Value.NE.Query(area, result, mask);
            _quadrants.Value.SW.Query(area, result, mask);
            _quadrants.Value.SE.Query(area, result, mask);
        }
    }

    public bool TryGetBounds(T item, out Bounds bounds) {
        if (_quadTreeEntries.TryGetValue(item, out var quadTreeEntry)) {
            bounds = quadTreeEntry.Bounds;
            return true;
        }
        bounds = default;
        return false;
    }
    public Bounds GetBounds(T item) {
        return TryGetBounds(item, out Bounds bounds) ? bounds : throw new LSException($"{item} not exist.");
    }
    public QuadTree<T>? GetQuadrant(Bounds bounds) {
        if (!_quadrants.HasValue) return null;
        if (_quadrants.Value.NW.Bounds.Contains(bounds))
            return _quadrants.Value.NW;
        else if (_quadrants.Value.NE.Bounds.Contains(bounds))
            return _quadrants.Value.NE;
        else if (_quadrants.Value.SW.Bounds.Contains(bounds))
            return _quadrants.Value.SW;
        else if (_quadrants.Value.SE.Bounds.Contains(bounds))
            return _quadrants.Value.SE;
        return null;
    }
    /// <summary>
    /// Remove um objeto da árvore.
    /// </summary>
    public bool Remove(T item) {
        if (!_quadTreeEntries.TryGetValue(item, out var entry))
            return false;

        // Garante que estamos no nó dono do item
        if (entry.Node != this)
            return entry.Node.Remove(item);

        if (!_items.Remove(item))
            throw new LSException($"Cannot remove {item} from node items.");

        _quadTreeEntries.Remove(item);

        // Tenta colapsar a partir deste nó para cima
        TryCollapseUpwards();

        return true;
    }

    private void TryCollapseUpwards() {
        // Se este nó ainda tem itens locais, não colapsa
        if (_items.Count > 0)
            return;

        // Se tem filhos, só colapsa se todos estiverem vazios
        if (_quadrants.HasValue) {
            if (!IsEmptyRecursive())
                return;

            // Descartar filhos
            _quadrants = null;
        }

        // Nós raiz não têm pai para notificar
        _parent?.TryCollapseUpwards();
    }

    private bool IsEmptyRecursive() {
        if (_items.Count > 0)
            return false;

        if (!_quadrants.HasValue)
            return true;

        return _quadrants.Value.NW.IsEmptyRecursive()
            && _quadrants.Value.NE.IsEmptyRecursive()
            && _quadrants.Value.SW.IsEmptyRecursive()
            && _quadrants.Value.SE.IsEmptyRecursive();
    }

    /// <summary>
    /// Remove todos os objetos da árvore.
    /// </summary>
    public void Clear() {
        clearNode();
        _quadTreeEntries.Clear();
    }
    internal int GetSubtreeCount() {
        int count = _items.Count;
        if (_quadrants.HasValue) {
            count += _quadrants.Value.NW.GetSubtreeCount();
            count += _quadrants.Value.NE.GetSubtreeCount();
            count += _quadrants.Value.SW.GetSubtreeCount();
            count += _quadrants.Value.SE.GetSubtreeCount();
        }
        return count;
    }
}
