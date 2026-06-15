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
    private readonly Bounds _bounds;
    private readonly int _capacity;
    private readonly List<QuadTreeEntry> _entries;
    //private QuadTree<T>[]? _children;
    private QuadTree<T>? _nw, _ne, _sw, _se;
    private bool _isDivided;
    private int _count;
    private readonly Dictionary<T, QuadTree<T>> _itemNodes;
    private readonly Dictionary<T, Bounds> _itemBounds;
    private readonly QuadTree<T>? _parent;

    /// <summary>
    /// Número total de objetos na árvore (incluindo subnós).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Limites espaciais desta árvore.
    /// </summary>
    public Bounds Bounds => _bounds;

    /// <summary>
    /// Capacidade máxima de objetos antes de subdividir.
    /// </summary>
    public int Capacity => _capacity;
    protected QuadTree(QuadTree<T> parent, Bounds bounds, Dictionary<T, QuadTree<T>> itemNodes, Dictionary<T, Bounds> itemBounds) {
        _parent = parent;
        _bounds = bounds;
        _capacity = parent.Capacity;
        _itemNodes = itemNodes;
        _itemBounds = itemBounds;
        _entries = new List<QuadTreeEntry>(_capacity);
        _nw = _ne = _sw = _se = null;
        _isDivided = false;
        _count = 0;
    }
    /// <summary>
    /// Cria uma nova QuadTree.
    /// </summary>
    /// <param name="bounds">Limites espaciais da árvore.</param>
    /// <param name="capacity">Número máximo de objetos por nó antes de subdividir (padrão: 64).</param>
    public QuadTree(Bounds bounds, int capacity = 64) {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        _parent = null;
        _bounds = bounds;
        _capacity = capacity;
        _itemNodes = new Dictionary<T, QuadTree<T>>();
        _itemBounds = new Dictionary<T, Bounds>();
        _entries = new List<QuadTreeEntry>(capacity);
        _isDivided = false;
        _count = 0;
    }
    /// <summary>
    /// Insere um objeto na árvore.
    /// </summary>
    /// <param name="item">O objeto a ser inserido.</param>
    /// <param name="bounds">Os limites espaciais do objeto.</param>
    /// <param name="allowOverlap">Indica se sobreposições são permitidas.</param>
    /// <returns>True se inserido com sucesso, false caso contrário.</returns>
    public bool Insert(T item, Bounds bounds, bool allowOverlap = false) {
        if (!_bounds.Intersects(bounds))
            return false;

        if (!allowOverlap && hasCollision(bounds))
            return false;

        // Subdivide if at capacity and not yet divided
        if (!_isDivided && _entries.Count >= _capacity)
            subdivide();

        if (_isDivided) {
            QuadTree<T>? child = getContainingChild(bounds);
            if (child != null) {
                if (!child.Insert(item, bounds, allowOverlap)) return false;
                _count++;
                return true;
            }
        }

        // Leaf node with space
        _entries.Add(new QuadTreeEntry(item, bounds));
        _itemNodes[item] = this;
        _itemBounds[item] = bounds;
        _count++;
        return true;
    }

    /// <summary>
    /// Consulta objetos dentro de uma área.
    /// </summary>
    public IReadOnlyList<T> Query(Bounds area) {
        var result = new List<T>();
        query(area, result);
        return result.ToList();
    }
    public void Query(Bounds area, ICollection<T> result) {
        if (result == null) result = new List<T>();
        else if (result.Count > 0) result.Clear();
        query(area, result);
    }
    private void query(Bounds area, ICollection<T> result) {
        if (!_bounds.Intersects(area))
            return;

        foreach (var entry in _entries) {
            if (area.Intersects(entry.Bounds))
                result.Add(entry.Item);
        }

        if (_isDivided) {
            _nw!.query(area, result);
            _ne!.query(area, result);
            _sw!.query(area, result);
            _se!.query(area, result);
        }
    }
    /// <summary>
    /// Atualiza os limites de um objeto na árvore.
    /// </summary>
    public bool Update(T item, Bounds newBounds, bool allowOverlap = false) {
        if (!_itemNodes.TryGetValue(item, out var node)) return false;

        // Fast path: still fits in the same leaf node
        if (node.Bounds.Contains(newBounds)) {
            // update in-place
            if (!node.updateEntry(item, newBounds, allowOverlap)) return false;
        }
        if (!Remove(item, out Bounds oldBounds)) return false;

        if (Insert(item, newBounds, allowOverlap) == false) {
            Insert(item, oldBounds, allowOverlap); // Reverte para os limites antigos se falhar
            return false;
        }

        return true;
    }
    private bool updateEntry(T item, Bounds newBounds, bool allowOverlap = false) {
        if (!allowOverlap && hasCollision(newBounds)) return false;
        for (int i = 0; i < _entries.Count; i++) {
            if (EqualityComparer<T>.Default.Equals(_entries[i].Item, item)) {
                _entries[i] = new QuadTreeEntry(item, newBounds);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Remove um objeto da árvore.
    /// </summary>
    public bool Remove(T item, out Bounds oldBounds) {
        if (!_itemNodes.TryGetValue(item, out var node) ||
            !_itemBounds.TryGetValue(item, out oldBounds)) {
            oldBounds = default;
            return false;
        }

        if (!node.removeLocal(item))
            return false;

        _itemNodes.Remove(item);
        _itemBounds.Remove(item);

        for (var current = node; current != null; current = current._parent)
            current._count--;

        return true;
    }
    private bool removeLocal(T item) {
        for (int i = 0; i < _entries.Count; i++) {
            if (EqualityComparer<T>.Default.Equals(_entries[i].Item, item)) {
                int last = _entries.Count - 1;
                _entries[i] = _entries[last];   // swap with last
                _entries.RemoveAt(last);         // O(1) removal
                return true;
            }
        }
        return false;
    }
    /// <summary>
    /// Remove todos os objetos da árvore.
    /// </summary>
    public void Clear() {
        clearNode();
        _itemNodes.Clear();
        _itemBounds.Clear();
    }
    private void clearNode() {
        _entries.Clear();
        _nw = null;
        _ne = null;
        _sw = null;
        _se = null;
        _isDivided = false;
        _count = 0;
    }
    /// <summary>
    /// Subdivide este nó em 4 quadrantes.
    /// </summary>
    private void subdivide() {
        float x = _bounds.X;
        float y = _bounds.Y;
        float w = _bounds.Width / 2;
        float hw = w / 2;
        float h = _bounds.Height / 2;
        float hh = h / 2;

        var nw = new Bounds(x - hw, y - hh, w, h); // Northwest
        var ne = new Bounds(x + hw, y - hh, w, h); // Northeast
        var sw = new Bounds(x - hw, y + hh, w, h); // Southwest
        var se = new Bounds(x + hw, y + hh, w, h); // Southeast

        _nw = new QuadTree<T>(this, nw, _itemNodes, _itemBounds);
        _ne = new QuadTree<T>(this, ne, _itemNodes, _itemBounds);
        _sw = new QuadTree<T>(this, sw, _itemNodes, _itemBounds);
        _se = new QuadTree<T>(this, se, _itemNodes, _itemBounds);

        _isDivided = true;

        // Redistribui objetos existentes para os filhos
        var entriesToRedistribute = new List<QuadTreeEntry>(_entries);
        _entries.Clear();

        foreach (var entry in entriesToRedistribute) {
            QuadTree<T>? child = getContainingChild(entry.Bounds);
            if (child != null) {
                child.Insert(entry.Item, entry.Bounds);
                continue;
            }

            // Se cruza mais de um quadrante, mantém no pai.
            _entries.Add(entry);
        }
    }

    private QuadTree<T>? getContainingChild(Bounds bounds) {
        if (!_isDivided) {
            return null;
        }

        if (_nw != null && _nw.Bounds.Contains(bounds)) return _nw;
        if (_ne != null && _ne.Bounds.Contains(bounds)) return _ne;
        if (_sw != null && _sw.Bounds.Contains(bounds)) return _sw;
        if (_se != null && _se.Bounds.Contains(bounds)) return _se;

        return null;
    }

    private bool hasCollision(Bounds bounds) {
        if (!_bounds.Intersects(bounds))
            return false;
        foreach (var entry in _entries) {
            if (entry.Bounds.Intersects(bounds)) {
                return true;
            }
        }

        var child = getContainingChild(bounds);
        if (child != null) {
            return child.hasCollision(bounds);
        }
        return false;
    }

    private readonly record struct QuadTreeEntry(T Item, Bounds Bounds);
}
