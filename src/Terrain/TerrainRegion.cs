namespace LSUtils.Terrain;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Geometry.Triangulation;
using LSUtils.Spatial;

public class TerrainRegion<TTerrainType, TContentType> : ISpatialObject {
    private readonly HashSet<TerrainPatch<TTerrainType>> _patches = new();
    private readonly HashSet<TerrainContent<TContentType>> _contents = new();
    private readonly HashSet<TerrainRegion<TTerrainType, TContentType>> _children = new();
    private bool _coverageAreaDirty = true;
    private float _polygonCoverageArea;

    public System.Guid ID { get; } = System.Guid.NewGuid();
    public TerrainRegion<TTerrainType, TContentType>? Parent { get; private set; }
    public Bounds Bounds { get; private set; }
    /// <summary>Sum of patch areas. Overlapping portions are counted once per member.</summary>
    public float MembershipArea => _patches.Sum(patch => patch.Area);
    /// <summary>Compatibility alias for <see cref="MembershipArea"/>.</summary>
    public float Area => MembershipArea;
    /// <summary>Geometric union area of all Polygon2D patches.</summary>
    public float PolygonCoverageArea {
        get {
            if (_coverageAreaDirty) RecalculatePolygonCoverageArea();
            return _polygonCoverageArea;
        }
    }
    public IReadOnlyCollection<TerrainPatch<TTerrainType>> Patches => _patches;
    public IReadOnlyCollection<TerrainContent<TContentType>> Contents => _contents;
    public IReadOnlyCollection<TerrainRegion<TTerrainType, TContentType>> Children => _children;

    public TerrainRegion(IEnumerable<TerrainPatch<TTerrainType>>? patches = null) {
        if (patches != null) {
            foreach (var patch in patches) AddPatch(patch);
        }

        RecalculateBounds();
    }

    public bool AddPatch(TerrainPatch<TTerrainType> patch) {
        bool added = _patches.Add(patch);
        if (added) {
            patch.Changed += OnPatchChanged;
            _coverageAreaDirty = true;
            RecalculateBounds();
        }
        return added;
    }

    public bool RemovePatch(TerrainPatch<TTerrainType> patch) {
        bool removed = _patches.Remove(patch);
        if (removed) {
            patch.Changed -= OnPatchChanged;
            _coverageAreaDirty = true;
            RecalculateBounds();
        }
        return removed;
    }

    public bool AddContent(TerrainContent<TContentType> content) {
        bool added = _contents.Add(content);
        if (added) {
            content.Changed += OnContentChanged;
            RecalculateBounds();
        }
        return added;
    }

    public bool RemoveContent(TerrainContent<TContentType> content) {
        bool removed = _contents.Remove(content);
        if (removed) {
            content.Changed -= OnContentChanged;
            RecalculateBounds();
        }
        return removed;
    }

    public bool AddChild(TerrainRegion<TTerrainType, TContentType> child) {
        if (child == this) throw new LSArgumentException("A region cannot be its own child.", nameof(child));
        for (var ancestor = this; ancestor != null; ancestor = ancestor.Parent) {
            if (ReferenceEquals(ancestor, child)) {
                throw new LSArgumentException("Adding this child would create a region cycle.", nameof(child));
            }
        }
        if (ReferenceEquals(child.Parent, this)) return false;

        child.Parent?._children.Remove(child);
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

    private void OnPatchChanged(TerrainPatch<TTerrainType> _) {
        _coverageAreaDirty = true;
        RecalculateBounds();
    }

    private void OnContentChanged(TerrainContent<TContentType> _) {
        RecalculateBounds();
    }

    private void RecalculatePolygonCoverageArea() {
        var polygons = _patches.Select(patch => patch.Shape as Polygon2D).ToList();
        if (polygons.Any(polygon => polygon == null)) {
            throw new LSInvalidOperationException("PolygonCoverageArea requires every region patch to use Polygon2D.");
        }
        if (polygons.Count == 0) {
            _polygonCoverageArea = 0f;
            _coverageAreaDirty = false;
            return;
        }

        var constraints = new List<TriangulationConstraint>();
        foreach (var polygon in polygons.Cast<Polygon2D>()) {
            for (int index = 0; index < polygon.Vertices.Count; index++) {
                constraints.Add(new TriangulationConstraint(
                    polygon.Vertices[index],
                    polygon.Vertices[(index + 1) % polygon.Vertices.Count]));
            }
        }

        var triangulation = ConstrainedTriangulation2D.Triangulate(constraints);
        float area = 0f;
        foreach (var triangle in triangulation.Triangles) {
            var first = triangulation.Vertices[triangle.A];
            var second = triangulation.Vertices[triangle.B];
            var third = triangulation.Vertices[triangle.C];
            var centroid = (first + second + third) / 3f;
            if (!polygons.Any(polygon => polygon!.Contains(centroid.X, centroid.Y))) continue;
            area += System.MathF.Abs((second - first).Cross(third - first)) * 0.5f;
        }
        _polygonCoverageArea = area;
        _coverageAreaDirty = false;
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
