namespace LSUtils.Collision;

using System;
using System.Collections.Generic;
using LSUtils.Spatial;

/// <summary>
/// Spatially indexed collision registry. It performs broad-phase queries with
/// SpatialHashGrid and narrow-phase primitive checks with Collision2D.
/// </summary>
public sealed class CollisionWorld<T> where T : notnull {
    private sealed class Entry {
        public T Item { get; }
        public CollisionShape2D Shape { get; set; }
        public CollisionFilter Filter { get; set; }
        public Entry(T item, CollisionShape2D shape, CollisionFilter filter) {
            Item = item;
            Shape = shape;
            Filter = filter;
        }
    }

    private readonly SpatialHashGrid<Entry> _index;
    private readonly Dictionary<T, Entry> _entries = new();
    private readonly List<Entry> _candidates = new();

    public int Count => _entries.Count;

    public CollisionWorld(float cellSize) {
        _index = new SpatialHashGrid<Entry>(cellSize);
    }

    public bool Contains(T item) => _entries.ContainsKey(item);

    public bool Add(T item, CollisionShape2D shape, CollisionFilter filter) {
        if (_entries.ContainsKey(item)) return false;
        var entry = new Entry(item, shape, filter);
        _entries.Add(item, entry);
        _index.Insert(entry, shape.Bounds);
        return true;
    }

    public bool Update(T item, CollisionShape2D shape, CollisionFilter? filter = null) {
        if (!_entries.TryGetValue(item, out var entry)) return false;
        entry.Shape = shape;
        if (filter.HasValue) entry.Filter = filter.Value;
        return _index.Update(entry, shape.Bounds);
    }

    public bool Remove(T item) {
        if (!_entries.Remove(item, out var entry)) return false;
        _index.Remove(entry);
        return true;
    }

    public void QueryOverlap(CollisionShape2D shape, ICollection<T> result, CollisionFilter queryFilter) {
        _candidates.Clear();
        _index.Query(shape.Bounds, _candidates);
        foreach (var candidate in _candidates) {
            if (!queryFilter.CanCollideWith(candidate.Filter)) continue;
            if (Collision2D.Intersects(shape, candidate.Shape)) result.Add(candidate.Item);
        }
    }

    public void QueryOverlapContacts(CollisionShape2D shape, ICollection<CollisionContact<T>> result, CollisionFilter queryFilter) {
        _candidates.Clear();
        _index.Query(shape.Bounds, _candidates);
        foreach (var candidate in _candidates) {
            if (!queryFilter.CanCollideWith(candidate.Filter)
                || !Collision2D.TryGetContact(shape, candidate.Shape, out var point, out var normal, out var distance)) continue;
            result.Add(new CollisionContact<T>(candidate.Item, point, normal, distance));
        }
    }

    public bool TrySweepCircle(LSVector2 from, LSVector2 to, float radius, CollisionFilter queryFilter, out T? hit, Func<T, bool>? predicate = null) {
        hit = default;
        var sweepBounds = new Bounds(
            (from.X + to.X) / 2f,
            (from.Y + to.Y) / 2f,
            MathF.Abs(to.X - from.X) + radius * 2f,
            MathF.Abs(to.Y - from.Y) + radius * 2f);
        _candidates.Clear();
        _index.Query(sweepBounds, _candidates);

        var closestDistance = float.PositiveInfinity;
        var found = false;
        foreach (var candidate in _candidates) {
            if ((predicate != null && !predicate(candidate.Item))
                || !queryFilter.CanCollideWith(candidate.Filter)
                || !Collision2D.SweepCircle(from, to, radius, candidate.Shape)) continue;
            var dx = candidate.Shape.Bounds.X - from.X;
            var dy = candidate.Shape.Bounds.Y - from.Y;
            var distance = dx * dx + dy * dy;
            if (distance >= closestDistance) continue;
            closestDistance = distance;
            hit = candidate.Item;
            found = true;
        }
        return found;
    }
}
