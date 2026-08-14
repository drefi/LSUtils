namespace LSUtils.Tests.Geometry;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Geometry.Triangulation;
using NUnit.Framework;

[TestFixture]
public class ConstrainedTriangulation2DTests {
    [Test]
    public void Triangulate_PreservesPolygonBoundaryConstraints() {
        var constraints = Loop(new[] {
            new LSVector2(0, 0), new LSVector2(10, 0),
            new LSVector2(10, 10), new LSVector2(0, 10),
        });

        var result = ConstrainedTriangulation2D.Triangulate(constraints);

        Assert.That(result.Triangles, Has.Count.EqualTo(2));
        AssertConstraintsAreTriangleEdges(result);
    }

    [Test]
    public void Triangulate_NodesCrossingConstraintsAtIntersection() {
        var constraints = Loop(new[] {
            new LSVector2(0, 0), new LSVector2(10, 0),
            new LSVector2(10, 10), new LSVector2(0, 10),
        });
        constraints.Add(new TriangulationConstraint(new LSVector2(0, 5), new LSVector2(10, 5)));
        constraints.Add(new TriangulationConstraint(new LSVector2(5, 0), new LSVector2(5, 10)));

        var result = ConstrainedTriangulation2D.Triangulate(constraints);
        int intersection = result.Vertices.ToList().FindIndex(point => point == new LSVector2(5, 5));

        Assert.That(intersection, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.Constraints.Count(edge => edge.From == intersection || edge.To == intersection), Is.EqualTo(4));
        AssertConstraintsAreTriangleEdges(result);
    }

    [Test]
    public void Triangulate_NodesCollinearOverlapsWithoutDuplicateEdges() {
        var constraints = Loop(new[] {
            new LSVector2(0, 0), new LSVector2(10, 0),
            new LSVector2(10, 10), new LSVector2(0, 10),
        });
        constraints.Add(new TriangulationConstraint(new LSVector2(2, 0), new LSVector2(8, 0)));

        var result = ConstrainedTriangulation2D.Triangulate(constraints);

        Assert.That(result.Constraints.Distinct().Count(), Is.EqualTo(result.Constraints.Count));
        Assert.That(result.Vertices, Does.Contain(new LSVector2(2, 0)));
        Assert.That(result.Vertices, Does.Contain(new LSVector2(8, 0)));
        AssertConstraintsAreTriangleEdges(result);
    }

    private static List<TriangulationConstraint> Loop(IReadOnlyList<LSVector2> vertices) {
        return Enumerable.Range(0, vertices.Count)
            .Select(index => new TriangulationConstraint(vertices[index], vertices[(index + 1) % vertices.Count]))
            .ToList();
    }

    private static void AssertConstraintsAreTriangleEdges(ConstrainedTriangulationResult result) {
        var triangleEdges = result.Triangles
            .SelectMany(triangle => new[] { Normalize(triangle.A, triangle.B), Normalize(triangle.B, triangle.C), Normalize(triangle.C, triangle.A) })
            .ToHashSet();
        Assert.That(result.Constraints.All(triangleEdges.Contains), Is.True);
    }

    private static (int From, int To) Normalize(int from, int to) => from < to ? (from, to) : (to, from);
}
