namespace LSUtils.Terrain;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Spatial;

public class TerrainRegion<TTerrainType, TContentType> : ISpatialObject {
    private readonly HashSet<TerrainPatch<TTerrainType>> _patches = new();
    private readonly HashSet<TerrainContent<TContentType>> _contents = new();
    private readonly HashSet<TerrainRegion<TTerrainType, TContentType>> _children = new();

    public System.Guid ID { get; } = System.Guid.NewGuid();
    public TerrainRegion<TTerrainType, TContentType>? Parent { get; private set; }
    public Bounds Bounds { get; private set; }
    public float Area => _patches.Sum(p => p.Area);
    public IReadOnlyCollection<TerrainPatch<TTerrainType>> Patches => _patches;
    public IReadOnlyCollection<TerrainContent<TContentType>> Contents => _contents;
    public IReadOnlyCollection<TerrainRegion<TTerrainType, TContentType>> Children => _children;

    public TerrainRegion(IEnumerable<TerrainPatch<TTerrainType>>? patches = null) {
        if (patches != null) {
            foreach (var patch in patches) _patches.Add(patch);
        }

        RecalculateBounds();
    }

    public bool AddPatch(TerrainPatch<TTerrainType> patch) {
        bool added = _patches.Add(patch);
        if (added) RecalculateBounds();
        return added;
    }

    public bool RemovePatch(TerrainPatch<TTerrainType> patch) {
        bool removed = _patches.Remove(patch);
        if (removed) RecalculateBounds();
        return removed;
    }

    public bool AddContent(TerrainContent<TContentType> content) {
        bool added = _contents.Add(content);
        if (added) RecalculateBounds();
        return added;
    }

    public bool RemoveContent(TerrainContent<TContentType> content) {
        bool removed = _contents.Remove(content);
        if (removed) RecalculateBounds();
        return removed;
    }

    public bool AddChild(TerrainRegion<TTerrainType, TContentType> child) {
        if (child == this) throw new LSArgumentException("A region cannot be its own child.", nameof(child));
        bool added = _children.Add(child);
        if (added) child.Parent = this;
        return added;
    }

    public bool RemoveChild(TerrainRegion<TTerrainType, TContentType> child) {
        bool removed = _children.Remove(child);
        if (removed && child.Parent == this) child.Parent = null;
        return removed;
    }

    public void RecalculateBounds() {
        var bounds = _patches.Select(p => p.Bounds).Concat(_contents.Select(c => c.Bounds)).ToList();
        Bounds = bounds.Count == 0 ? default : Combine(bounds);
    }

    private static Bounds Combine(IReadOnlyList<Bounds> bounds) {
        float minX = bounds[0].MinX;
        float maxX = bounds[0].MaxX;
        float minY = bounds[0].MinY;
        float maxY = bounds[0].MaxY;

        for (int i = 1; i < bounds.Count; i++) {
            var item = bounds[i];
            if (item.MinX < minX) minX = item.MinX;
            if (item.MaxX > maxX) maxX = item.MaxX;
            if (item.MinY < minY) minY = item.MinY;
            if (item.MaxY > maxY) maxY = item.MaxY;
        }

        float width = maxX - minX;
        float height = maxY - minY;
        return new Bounds(minX + width * 0.5f, minY + height * 0.5f, width, height);
    }
}
