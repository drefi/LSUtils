namespace LSUtils.Terrain;

using System;
using System.Collections.Generic;
using LSUtils.Geometry;
using LSUtils.Spatial;

public class TerrainPatch<TTerrainType> : ISpatialObject {
    internal event Action<TerrainPatch<TTerrainType>>? Changed;

    public System.Guid ID { get; } = System.Guid.NewGuid();
    public long Version { get; private set; }
    public TTerrainType Type { get; private set; }
    public IShape2D Shape { get; private set; }
    public int Layer { get; private set; }
    public int Priority { get; private set; }
    public Bounds Bounds => Shape.Bounds;
    public float Area => Shape.Area;

    public TerrainPatch(TTerrainType type, IShape2D shape, int layer = 0, int priority = 0) {
        Type = type;
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
        Layer = layer;
        Priority = priority;
    }

    public bool Contains(float x, float y) {
        return Shape.Contains(x, y);
    }

    public void SetShape(IShape2D shape) {
        if (shape == null) throw new LSArgumentNullException(nameof(shape));
        if (ReferenceEquals(Shape, shape)) return;
        Shape = shape;
        NotifyChanged();
    }

    public void SetType(TTerrainType type) {
        if (EqualityComparer<TTerrainType>.Default.Equals(Type, type)) return;
        Type = type;
        NotifyChanged();
    }

    public void SetLayer(int layer) {
        if (Layer == layer) return;
        Layer = layer;
        NotifyChanged();
    }

    public void SetPriority(int priority) {
        if (Priority == priority) return;
        Priority = priority;
        NotifyChanged();
    }

    private void NotifyChanged() {
        Version++;
        Changed?.Invoke(this);
    }
}
