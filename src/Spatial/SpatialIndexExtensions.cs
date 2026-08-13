namespace LSUtils.Spatial;

using System.Collections.Generic;

public static class SpatialIndexExtensions {
    public static List<T> Query<T>(this ISpatialIndex<T> index, Bounds area, T[]? mask = null) where T : notnull {
        var result = new List<T>();
        index.Query(area, result, mask);
        return result;
    }

    public static bool Insert<T>(this ISpatialIndex<T> index, T item) where T : ISpatialObject {
        return index.Insert(item, item.Bounds);
    }

    public static bool Update<T>(this ISpatialIndex<T> index, T item) where T : ISpatialObject {
        return index.Update(item, item.Bounds);
    }
}
