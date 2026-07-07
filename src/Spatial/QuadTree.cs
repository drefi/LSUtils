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
    private (Bounds NW, Bounds NE, Bounds SW, Bounds SE) _quadrantBounds;

    private readonly Bounds _bounds;
    private readonly int _capacity;
    //private QuadTree<T>? _nw, _ne, _sw, _se;
    //private SubQuadTree? _quadrant;
    //private bool _isDivided;
    private int _count;
    public readonly record struct QuadTreeEntry(QuadTree<T> Node, Bounds Bounds);
    //private readonly Dictionary<T, QuadTree<T>> _entryNodes;
    //private readonly Dictionary<T, Bounds> _entryBounds;
    private readonly Dictionary<T, QuadTreeEntry> _quadTreeEntries = new();
    private readonly HashSet<T> _items = new();
    private readonly QuadTree<T>? _parent;

    /// <summary>
    /// Número total de objetos na árvore.
    /// </summary>
    public int Count {
        get {
            return _quadTreeEntries.Count;
        }
    }

    /// <summary>
    /// Limites espaciais desta árvore.
    /// </summary>
    public Bounds Bounds => _bounds;

    /// <summary>
    /// Capacidade máxima de objetos antes de subdividir.
    /// </summary>
    public int Capacity => _capacity;

    protected QuadTree(QuadTree<T> parent, Bounds bounds, Dictionary<T, QuadTreeEntry> entries) {
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
            new Bounds(_bounds.X - hw, _bounds.Y - hh, w, h),
            new Bounds(_bounds.X + hw, _bounds.Y - hh, w, h),
            new Bounds(_bounds.X - hw, _bounds.Y + hh, w, h),
            new Bounds(_bounds.X + hw, _bounds.Y + hh, w, h));
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
        _quadTreeEntries = new();
        _quadrants = null;
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
        _count = 0;
    }
    /// <summary>
    /// Insere um objeto na árvore.
    /// </summary>
    /// <param name="item">O objeto a ser inserido.</param>
    /// <param name="bounds">Os limites espaciais do objeto.</param>
    /// <returns>True se inserido com sucesso, false caso contrário.</returns>
    public bool InsertOrUpdate(T item, Bounds bounds) {
        // out of this quadtree bounds
        if (!_bounds.Intersects(bounds))
            return false;

        // Subdivide if at capacity and not yet divided
        if (!_quadrants.HasValue && _count >= _capacity) {
            _quadrants = (new QuadTree<T>(this, _quadrantBounds.NW, _quadTreeEntries),
                new QuadTree<T>(this, _quadrantBounds.NE, _quadTreeEntries),
                new QuadTree<T>(this, _quadrantBounds.SW, _quadTreeEntries),
                new QuadTree<T>(this, _quadrantBounds.SE, _quadTreeEntries)
            );
            var entriesToRemove = new List<T>();

            foreach (var currItem in _items) {
                if (_quadTreeEntries.TryGetValue(currItem, out var quadTreeEntry) == false) throw new LSException($"{currItem} entry not found.");
                var child = GetQuadrant(quadTreeEntry.Bounds);
                if (child == null) continue;
                if (child.InsertOrUpdate(currItem, quadTreeEntry.Bounds) == false) throw new LSException($"{currItem} cannot be inserted in child {child}.");
                //_count++;
                entriesToRemove.Add(currItem);
            }
            //remove only entries that where added to children
            _count -= _items.RemoveWhere(e => entriesToRemove.Contains(e));

        }

        if (_quadrants.HasValue) {
            var child = GetQuadrant(bounds);
            if (child != null && child.InsertOrUpdate(item, bounds)) {
                //_count++;
                return true;
            }
        }
        // Leaf node
        _items.Add(item);
        _quadTreeEntries[item] = new QuadTreeEntry(this, bounds);
        _count++;
        return true;
    }
    /// <summary>
    /// Consulta objetos dentro de uma área.
    /// </summary>
    public IReadOnlyList<T> Query(Bounds area, HashSet<T>? ignore = null) {
        var result = new HashSet<T>();
        Query(area, result, ignore);
        return result.ToList();
    }
    public void Query(Bounds area, ICollection<T> result, HashSet<T>? ignore = null) {
        HashSet<T> seen = ignore == null ? new HashSet<T>() : ignore;
        if (!_bounds.Intersects(area))
            return;

        foreach (var item in _items) {
            if (_quadTreeEntries.TryGetValue(item, out var quadTreeEntry) == false) throw new LSException($"_items[{_items.Count}]: {item} does not exist in _quadTreeEntries[{_quadTreeEntries.Count}].");
            if (area.Intersects(quadTreeEntry.Bounds)) {
                if (!seen.Add(item)) continue;
                result.Add(item);
            }
        }

        if (_quadrants.HasValue) {
            _quadrants.Value.NW.Query(area, result);
            _quadrants.Value.NE.Query(area, result);
            _quadrants.Value.SW.Query(area, result);
            _quadrants.Value.SE.Query(area, result);
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
        if (!_quadTreeEntries.TryGetValue(item, out var quadTreeEntry)) {
            return false;
        }
        if (quadTreeEntry.Node != this)
            return quadTreeEntry.Node.Remove(item);
        if (!_items.Remove(item)) throw new LSException($"Cannot remove {item} from entries.");
        return _quadTreeEntries.Remove(item);
    }

    /// <summary>
    /// Remove todos os objetos da árvore.
    /// </summary>
    public void Clear() {
        clearNode();
        _quadTreeEntries.Clear();
    }
}
