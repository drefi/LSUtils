namespace LSUtils.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

/// <summary>Boolean set operations for LSUtils polygonal shapes.</summary>
public static class LSPolygonBoolean2D {
    public const int DefaultCoordinatePrecision = 5;

    public static IReadOnlyList<LSPolygonArea2D> Union(
        IEnumerable<ILSPolygonalShape2D> shapes,
        int coordinatePrecision = DefaultCoordinatePrecision) =>
        Execute(ClipType.Union, shapes, Array.Empty<ILSPolygonalShape2D>(), coordinatePrecision);

    public static IReadOnlyList<LSPolygonArea2D> Intersection(
        IEnumerable<ILSPolygonalShape2D> subjects,
        IEnumerable<ILSPolygonalShape2D> clips,
        int coordinatePrecision = DefaultCoordinatePrecision) =>
        Execute(ClipType.Intersection, subjects, clips, coordinatePrecision);

    public static IReadOnlyList<LSPolygonArea2D> Difference(
        IEnumerable<ILSPolygonalShape2D> subjects,
        IEnumerable<ILSPolygonalShape2D> clips,
        int coordinatePrecision = DefaultCoordinatePrecision) =>
        Execute(ClipType.Difference, subjects, clips, coordinatePrecision);

    private static IReadOnlyList<LSPolygonArea2D> Execute(
        ClipType operation,
        IEnumerable<ILSPolygonalShape2D> subjects,
        IEnumerable<ILSPolygonalShape2D> clips,
        int coordinatePrecision) {
        ArgumentNullException.ThrowIfNull(subjects);
        ArgumentNullException.ThrowIfNull(clips);
        if (coordinatePrecision is < -8 or > 8)
            throw new ArgumentOutOfRangeException(nameof(coordinatePrecision));

        var subjectPaths = ToPaths(subjects);
        if (subjectPaths.Count == 0) return Array.Empty<LSPolygonArea2D>();
        var clipPaths = ToPaths(clips);
        var clipper = new ClipperD(coordinatePrecision);
        clipper.AddSubject(subjectPaths);
        if (clipPaths.Count > 0) clipper.AddClip(clipPaths);

        var tree = new PolyTreeD();
        if (!clipper.Execute(operation, FillRule.NonZero, tree))
            throw new InvalidOperationException("Polygon boolean operation failed.");

        var result = new List<LSPolygonArea2D>();
        CollectAreas(tree, result);
        return result.AsReadOnly();
    }

    private static PathsD ToPaths(IEnumerable<ILSPolygonalShape2D> shapes) {
        var paths = new PathsD();
        foreach (var shape in shapes) {
            ArgumentNullException.ThrowIfNull(shape);
            paths.Add(ToPath(shape.OuterBoundary, clockwise: false));
            foreach (var hole in shape.Holes)
                paths.Add(ToPath(hole, clockwise: true));
        }
        return paths;
    }

    private static PathD ToPath(LSPolygon2D polygon, bool clockwise) {
        var vertices = polygon.Vertices;
        var reverse = polygon.IsClockwise != clockwise;
        var path = new PathD(vertices.Count);
        for (var index = 0; index < vertices.Count; index++) {
            var vertex = vertices[reverse ? vertices.Count - index - 1 : index];
            if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y))
                throw new ArgumentException("Polygon coordinates must be finite.", nameof(polygon));
            path.Add(new PointD(vertex.X, vertex.Y));
        }
        return path;
    }

    private static void CollectAreas(PolyPathD parent, ICollection<LSPolygonArea2D> result) {
        for (var index = 0; index < parent.Count; index++) {
            var outerNode = parent[index];
            if (outerNode.IsHole || outerNode.Polygon is null) continue;

            var holes = new List<LSPolygon2D>();
            for (var holeIndex = 0; holeIndex < outerNode.Count; holeIndex++) {
                var holeNode = outerNode[holeIndex];
                if (holeNode.IsHole && holeNode.Polygon is { } holePath)
                    holes.Add(ToPolygon(holePath));
            }

            result.Add(new LSPolygonArea2D(ToPolygon(outerNode.Polygon), holes));

            for (var holeIndex = 0; holeIndex < outerNode.Count; holeIndex++) {
                var holeNode = outerNode[holeIndex];
                if (holeNode.IsHole) CollectAreas(holeNode, result);
            }
        }
    }

    private static LSPolygon2D ToPolygon(PathD path) =>
        new(path.Select(point => new LSVector2((float)point.x, (float)point.y)));
}
