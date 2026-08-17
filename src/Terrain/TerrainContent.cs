namespace LSUtils.Terrain;

using System;
using System.Collections.Generic;
using LSUtils.Geometry;
using LSUtils.Spatial;

public class TerrainContent<TContentType> : ISpatialObject {
    internal event Action<TerrainContent<TContentType>>? Changed;

    public System.Guid ID { get; } = System.Guid.NewGuid();
    public long Version { get; private set; }
    public TContentType Type { get; private set; }
    public IShape2D Shape { get; private set; }
    public TerrainContentMobility Mobility { get; private set; }
    public Bounds Bounds => Shape.Bounds;
    public float Area => Shape.Area;

    public TerrainContent(TContentType type, IShape2D shape, TerrainContentMobility mobility = TerrainContentMobility.Static) {
        Type = type;
        Shape = shape ?? throw new LSArgumentNullException(nameof(shape));
        Mobility = mobility;
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

    public void SetType(TContentType type) {
        if (EqualityComparer<TContentType>.Default.Equals(Type, type)) return;
        Type = type;
        NotifyChanged();
    }

    public void SetMobility(TerrainContentMobility mobility) {
        if (Mobility == mobility) return;
        Mobility = mobility;
        NotifyChanged();
    }

    private void NotifyChanged() {
        Version++;
        Changed?.Invoke(this);
    }
}
