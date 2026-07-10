using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LSUtils.Spatial;

public class SpatialHashGrid<T> : ISpatialIndex<T> where T : notnull {
    private readonly Dictionary<long, Cell> _cells = new();
    private readonly Dictionary<T, Entry> _entries = new();
    private uint _queryId;
    private float _cellSize;
    private float _invCellSize;
    public int Count => _entries.Count;

    public SpatialHashGrid(float cellSize) {
        if (cellSize <= 0) throw new LSArgumentException($"cellSize cennot be {cellSize}");
        _cellSize = cellSize;
        _invCellSize = 1f / cellSize;
    }

    public void Clear() {
        foreach (var cell in _cells.Values)
            cell.Entries.Clear();

        _cells.Clear();
        _entries.Clear();
    }

    public bool Insert(T item, Bounds bounds) {

        if (!_entries.TryGetValue(item, out var entry)) {
            int newMinX = ToCell(bounds.MinX);
            int newMaxX = ToCell(bounds.MaxX);
            int newMinY = ToCell(bounds.MinY);
            int newMaxY = ToCell(bounds.MaxY);

            entry = new Entry(item, bounds);

            _entries.Add(item, entry);

            AddToCells(entry,
                newMinX, newMaxX,
                newMinY, newMaxY);

            return true;
        }
        return false;
    }
    private void AddToCells(
        Entry entry,
        int minX,
        int maxX,
        int minY,
        int maxY) {
        entry.CellKeys.Clear();

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                long key = MakeKey(x, y);

                if (!_cells.TryGetValue(key, out var cell)) {
                    cell = new Cell(x, y);
                    _cells.Add(key, cell);
                }

                cell.Entries.Add(entry);

                entry.CellKeys.Add(key);
            }
        }
    }
    private void RemoveFromCells(Entry entry) {
        foreach (long key in entry.CellKeys) {
            if (!_cells.TryGetValue(key, out var cell))
                continue;

            cell.Entries.Remove(entry);

            // Opcional: remover células vazias
            if (cell.Entries.Count == 0)
                _cells.Remove(key);
        }

        entry.CellKeys.Clear();
    }
    public void Query(Bounds area, ICollection<T> result) {
        _queryId++;

        int minX = ToCell(area.MinX);
        int maxX = ToCell(area.MaxX);
        int minY = ToCell(area.MinY);
        int maxY = ToCell(area.MaxY);

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                if (!_cells.TryGetValue(MakeKey(x, y), out var cell))
                    continue;

                var entries = cell.Entries;

                for (int i = 0; i < entries.Count; i++) {
                    var entry = entries[i];

                    if (entry.LastQueryId == _queryId)
                        continue;

                    entry.LastQueryId = _queryId;

                    if (entry.Bounds.Intersects(area))
                        result.Add(entry.Item);
                }
            }
        }
    }
    public delegate void PairAction(Entry EntryA, Entry EntryB, Cell Cell);
    private record struct CellPair(Entry EntryA, Entry EntryB) {
        public bool Equals(CellPair other) {
            return (EqualityComparer<Entry>.Default.Equals(EntryA, other.EntryA) && EqualityComparer<Entry>.Default.Equals(EntryB, other.EntryB)) ||
                   (EqualityComparer<Entry>.Default.Equals(EntryA, other.EntryB) && EqualityComparer<Entry>.Default.Equals(EntryB, other.EntryA));
        }
        public override int GetHashCode() {
            // Usamos XOR (^) ou combinamos os hashes de forma que a ordem não importe
            int hashA = EntryA?.GetHashCode() ?? 0;
            int hashB = EntryB?.GetHashCode() ?? 0;

            // Garante que GetHashCode(1, 2) seja igual a GetHashCode(2, 1)
            return hashA ^ hashB;
        }
    }
    public void ForEachPotentialPair(PairAction callback) {
        HashSet<CellPair> seen = new();
        foreach (var cell in _cells.Values) {
            int count = cell.Entries.Count;
            for (int i = 0; i < count - 1; i++) {
                var entryA = cell.Entries[i];
                for (int j = i + 1; j < count; j++) {
                    var entryB = cell.Entries[j];
                    if (!seen.Add(new CellPair(entryA, entryB))) continue;
                    callback(entryA, entryB, cell);
                }
            }
        }
    }
    public bool Remove(T item) {
        if (!_entries.TryGetValue(item, out var entry))
            return false;

        RemoveFromCells(entry);

        _entries.Remove(item);

        return true;
    }

    public bool TryGetBounds(T item, out Bounds bounds) {
        if (!_entries.TryGetValue(item, out var entry) || entry == null) {
            bounds = default;
            return false;
        }
        bounds = entry.Bounds;
        return true;
    }
    public Bounds GetBounds(T item) {
        return TryGetBounds(item, out var bounds) ? bounds : throw new LSException($"{item} not found.");
    }

    private int ToCell(float value) {
        return (int)LSMath.Floor(value * _invCellSize);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long MakeKey(int x, int y) {
        return ((long)x << 32) | (uint)y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeKey(long key, out int x, out int y) {
        x = (int)(key >> 32);
        y = (int)key;
    }

    public sealed class Cell {
        public readonly long CellKey;
        public readonly int X;
        public readonly int Y;

        public readonly List<Entry> Entries = new();

        public Cell(int x, int y) {
            X = x;
            Y = y;
            CellKey = MakeKey(X, Y);
        }
    }

    public sealed class Entry {
        public readonly T Item;
        public readonly Bounds Bounds;

        public uint LastQueryId;

        // células ocupadas
        public readonly List<long> CellKeys = new();

        internal Entry(T item, Bounds bounds) {
            Item = item;
            Bounds = bounds;
        }
    }
}
