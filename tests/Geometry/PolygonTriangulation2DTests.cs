namespace LSUtils.Tests.Geometry;

using System;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Geometry.Triangulation;
using NUnit.Framework;

[TestFixture]
public class PolygonTriangulation2DTests {
    [Test]
    public void Triangulate_ExcludesTrianglesInsideHole() {
        var area = new PolygonArea2D(Rectangle(0, 0, 20, 20), new[] { Rectangle(7, 6, 6, 8) });

        var result = PolygonTriangulation2D.Triangulate(area);

        Assert.That(result.Triangles, Is.Not.Empty);
        Assert.That(result.Triangles.All(triangle => {
            var centroid = Centroid(result, triangle);
            return area.Contains(centroid.X, centroid.Y);
        }), Is.True);
        Assert.That(TriangleArea(result), Is.EqualTo(area.Area).Within(0.001f));
    }

    [Test]
    public void Triangulate_PreservesEveryBoundaryLoop() {
        var area = new PolygonArea2D(Rectangle(0, 0, 20, 20), new[] {
            Rectangle(3, 3, 4, 5),
            Rectangle(13, 11, 4, 6),
        });

        var result = PolygonTriangulation2D.Triangulate(area);
        var triangleEdges = result.Triangles.SelectMany(triangle => new[] {
            Normalize(triangle.A, triangle.B), Normalize(triangle.B, triangle.C), Normalize(triangle.C, triangle.A),
        }).ToHashSet();

        Assert.That(result.Constraints.All(triangleEdges.Contains), Is.True);
        Assert.That(TriangleArea(result), Is.EqualTo(area.Area).Within(0.001f));
    }

    private static Polygon2D Rectangle(float x, float y, float width, float height) {
        return new Polygon2D(new[] {
            new LSVector2(x, y), new LSVector2(x + width, y),
            new LSVector2(x + width, y + height), new LSVector2(x, y + height),
        });
    }

    private static LSVector2 Centroid(PolygonTriangulationResult result, TriangulationTriangle triangle) {
        return (result.Vertices[triangle.A] + result.Vertices[triangle.B] + result.Vertices[triangle.C]) / 3f;
    }

    private static float TriangleArea(PolygonTriangulationResult result) {
        return result.Triangles.Sum(triangle => {
            var first = result.Vertices[triangle.A];
            var second = result.Vertices[triangle.B];
            var third = result.Vertices[triangle.C];
            return MathF.Abs((second - first).Cross(third - first)) * 0.5f;
        });
    }

    private static (int From, int To) Normalize(int from, int to) => from < to ? (from, to) : (to, from);
}
