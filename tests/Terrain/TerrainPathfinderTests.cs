namespace LSUtils.Tests.Terrain;

using System;
using NUnit.Framework;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Spatial;
using LSUtils.Terrain;
using LSUtils.Terrain.Navigation;

[TestFixture]
public class TerrainPathfinderTests {
    [Test]
    public void NavigationMesh_BakesWhiteHorseExampleGeometry() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(576, 324, 1056, 560), TerrainType.Water);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, Rectangle(120, 120, 850, 410)));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(170, 160, 325, 310), priority: 1));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(575, 260, 240, 145), layer: 1));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(221, 206, 48, 48)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(326, 321, 48, 48)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(505, 435, 40, 40)));
        var settings = new TerrainNavigationSettings<TerrainType, ContentType>(
            patch => patch?.Type switch {
                null => 0f,
                TerrainType.Water => 0f,
                TerrainType.Mud => 1.4f,
                _ => 1f,
            },
            agentRadius: 1f);

        var mesh = world.BakeNavigationMesh(settings);

        Assert.That(mesh.Triangles, Is.Not.Empty);
        Assert.That(mesh.Triangles.All(IsNonDegenerate), Is.True, DescribeDegenerateTriangles(mesh.Triangles));
    }

    [Test]
    public void NavigationMesh_BakesWhenPassablePatchCutsAcrossOtherPatches() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(576, 324, 1056, 560), TerrainType.Water);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, new Polygon2D(new[] {
            new LSVector2(98, 76), new LSVector2(1022, 130),
            new LSVector2(1017, 321), new LSVector2(119, 530),
        })));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(170, 117, 325, 353), priority: 1));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(575, 260, 240, 145), layer: 1));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(221, 206, 48, 48)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(326, 321, 48, 48)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(505, 435, 40, 40)));
        var settings = new TerrainNavigationSettings<TerrainType, ContentType>(
            patch => patch?.Type switch {
                null => 0f,
                TerrainType.Water => 0f,
                TerrainType.Mud => 1.4f,
                _ => 1f,
            },
            agentRadius: 1f);

        var mesh = world.BakeNavigationMesh(settings);

        Assert.That(mesh.Triangles, Is.Not.Empty);
        Assert.That(mesh.Triangles.All(IsNonDegenerate), Is.True, DescribeDegenerateTriangles(mesh.Triangles));
    }

    [Test]
    [Category("Performance")]
    public void NavigationMesh_BuildStatistics_ExposeVisibilityGraphGrowth() {
        var world = CreateWorld();
        const int obstacleCount = 16;
        for (int index = 0; index < obstacleCount; index++) {
            int column = index % 4;
            int row = index / 4;
            world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(10 + column * 22, 10 + row * 22, 10, 10)));
        }

        var mesh = world.BuildNavigationMesh(Settings());
        var stats = mesh.BuildStatistics;
        TestContext.Progress.WriteLine($"Navigation mesh: obstacles={stats.ObstacleCount}, nodes={stats.NodeCount}, pairs={stats.VisibilityTests}, edges={stats.EdgeCount}, candidates={stats.ObstacleCandidateChecks}, costSamples={stats.TerrainCostSamples}, elapsedMs={stats.Elapsed.TotalMilliseconds:F1}");

        Assert.That(stats.ObstacleCount, Is.EqualTo(obstacleCount));
        Assert.That(stats.VisibilityTests, Is.LessThan((long)stats.NodeCount * 24));
        Assert.That(stats.TerrainCostSamples, Is.GreaterThan(0));
    }

    [Test]
    [Category("Performance")]
    public void NavigationMesh_RepeatedQueries_DoNotConnectEndpointsToEveryNode() {
        var world = CreateWorld();
        for (int index = 0; index < 16; index++) {
            int column = index % 4;
            int row = index / 4;
            world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(14 + column * 22, 14 + row * 22, 8, 8)));
        }
        var mesh = world.BuildNavigationMesh(Settings());
        mesh.FindPath(new LSVector2(5, 5), new LSVector2(95, 95));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int index = 0; index < 40; index++) {
            float offset = index % 8;
            mesh.FindPath(new LSVector2(5, 5 + offset), new LSVector2(95, 95 - offset));
        }
        stopwatch.Stop();
        double averageMilliseconds = stopwatch.Elapsed.TotalMilliseconds / 40d;
        TestContext.Progress.WriteLine($"Navigation query average: {averageMilliseconds:F2} ms across {mesh.NodeCount} nodes");

        Assert.That(averageMilliseconds, Is.LessThan(50d));
    }

    [Test]
    public void FindPath_AvoidsImpassableWaterWithClearance() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(40, 20, 20, 60), layer: 1));

        var path = world.FindPath(new LSVector2(20, 50), new LSVector2(80, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Any(point => point.Y < 16f || point.Y > 84f), Is.True);
        Assert.That(path.All(point => !Rectangle(40, 20, 20, 60).Contains(point.X, point.Y)), Is.True);
    }

    [Test]
    public void FindPath_ChoosesLowerTerrainCostWhenAlternativeExists() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(40, 20, 20, 60), layer: 1));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, Rectangle(20, 0, 60, 20), layer: 1));

        var path = world.FindPath(new LSVector2(20, 50), new LSVector2(80, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Any(point => point.Y > 80f), Is.True);
    }

    [Test]
    public void NavigationMesh_IncludesPassableCostBoundaryVertices() {
        var world = CreateWorld();
        var mud = Rectangle(35, 30, 30, 40);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, mud, layer: 1));

        var mesh = world.BuildNavigationMesh(Settings());

        Assert.That(mud.Vertices.All(vertex => mesh.Nodes.Contains(vertex)), Is.True);
    }

    [Test]
    public void NavigationMesh_ConstrainedTrianglesDoNotCrossOverlappingPatchBoundaries() {
        var world = CreateWorld();
        var mud = Rectangle(20, 20, 55, 55);
        var grass = Rectangle(45, 35, 45, 50);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, mud, layer: 1));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, grass, layer: 2));

        var mesh = world.BuildNavigationMesh(Settings());

        Assert.That(mesh.Triangles.Any(triangle => triangle.Cost == 8f), Is.True);
        Assert.That(mesh.Triangles.Any(triangle => triangle.Cost == 1f), Is.True);
        foreach (var triangle in mesh.Triangles) {
            AssertTriangleDoesNotCrossBoundary(triangle, mud);
            AssertTriangleDoesNotCrossBoundary(triangle, grass);
        }
    }

    [Test]
    public void NavigationMesh_ExposesDominantPatchAndCostPerTriangle() {
        var world = CreateWorld();
        var mudPatch = new TerrainPatch<TerrainType>(TerrainType.Mud, Rectangle(30, 25, 40, 50), layer: 1);
        world.AddPatch(mudPatch);

        var mesh = world.BuildNavigationMesh(Settings());
        int mudTriangle = Enumerable.Range(0, mesh.Triangles.Count).First(index => mesh.Triangles[index].Cost == 8f);

        Assert.That(mesh.GetTrianglePatch(mudTriangle), Is.SameAs(mudPatch));
        Assert.That(mesh.Triangles[mudTriangle].Cost, Is.EqualTo(8f));
    }

    [Test]
    public void FindPath_AvoidsExpensivePassablePatchWithoutObstacle() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, Rectangle(35, 35, 30, 30), layer: 1));
        var mesh = world.BuildNavigationMesh(Settings());

        var path = mesh.FindPath(new LSVector2(10, 50), new LSVector2(90, 50));

        Assert.That(path, Has.Count.GreaterThan(2));
        Assert.That(path.Any(point => point.Y <= 35f || point.Y >= 65f), Is.True);
    }

    [Test]
    public void NavigationMesh_IncludesOuterVerticesOfStandalonePassablePatch() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(50, 50, 100, 100), TerrainType.Water);
        var grass = Rectangle(10, 10, 60, 60);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, grass));
        var settings = new TerrainNavigationSettings<TerrainType, ContentType>(
            patch => patch?.Type == TerrainType.Grass ? 1f : 0f,
            agentRadius: 0f);

        var mesh = world.BakeNavigationMesh(settings);

        Assert.That(grass.Vertices.All(vertex => mesh.Nodes.Contains(vertex)), Is.True);
    }

    [Test]
    public void FindPath_AvoidsBlockingContent() {
        var world = CreateWorld();
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(42, 35, 16, 30)));

        var path = world.FindPath(new LSVector2(15, 50), new LSVector2(85, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path, Has.Count.LessThanOrEqualTo(6));
        Assert.That(path.Zip(path.Skip(1), (from, to) => SegmentIntersectsRectangle(from, to, 42, 35, 16, 30)).All(intersects => !intersects), Is.True);
    }

    [Test]
    public void FindPath_AcrossPassableTriangleBoundaries_DoesNotZigZag() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(20, 0, 20, 100), layer: 1));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(60, 0, 20, 100), layer: 1));
        var mesh = world.BuildNavigationMesh(Settings());

        var path = mesh.FindPath(new LSVector2(10, 20), new LSVector2(90, 20));

        Assert.That(path, Has.Count.EqualTo(2));
    }

    [Test]
    public void FindPath_BetweenRoomWalls_ChoosesNearSideOfObstacle() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(576, 324, 1056, 560), TerrainType.Water);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(70, 145, 1010, 430)));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, new Polygon2D(new[] {
            new LSVector2(785, 245), new LSVector2(965, 225),
            new LSVector2(1010, 355), new LSVector2(815, 385),
        }), layer: 1));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(690, 405, 14, 150)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(735, 430, 130, 14)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(935, 430, 100, 14)));
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(700, 500, 90, 14)));
        var settings = new TerrainNavigationSettings<TerrainType, ContentType>(
            patch => patch?.Type switch { TerrainType.Water => 0f, _ => 1f },
            agentRadius: 8f,
            clearanceArcSegments: 2);
        var mesh = world.BakeNavigationMesh(settings);

        var path = mesh.FindPath(new LSVector2(752, 272), new LSVector2(748, 556));
        TestContext.Progress.WriteLine($"Room route: {string.Join(" -> ", path)}");

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Max(point => point.X), Is.LessThan(900f));
    }

    [Test]
    public void FindPath_FromWorldCorner_ReachesDestinationBehindObstacle() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(545, 325, 850, 410), TerrainType.Grass);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(120, 120, 850, 410)));
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(575, 260, 240, 145), layer: 1));
        var mesh = world.BuildNavigationMesh(Settings());

        var path = mesh.FindPath(new LSVector2(823, 331), new LSVector2(500, 331));

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Zip(path.Skip(1), (from, to) => SegmentIntersectsRectangle(from, to, 575, 260, 240, 145)).All(intersects => !intersects), Is.True);
    }

    [Test]
    public void NavigationMesh_ReusesTopologyForMultiplePathQueries() {
        var world = CreateWorld();
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(42, 35, 16, 30)));
        var mesh = world.BuildNavigationMesh(Settings());

        var firstPath = mesh.FindPath(new LSVector2(15, 50), new LSVector2(85, 50));
        var secondPath = mesh.FindPath(new LSVector2(15, 20), new LSVector2(85, 80));

        Assert.That(mesh.IsCurrent, Is.True);
        Assert.That(mesh.NodeCount, Is.GreaterThan(0));
        Assert.That(mesh.Nodes, Has.Count.EqualTo(mesh.NodeCount));
        Assert.That(mesh.Edges, Has.Count.EqualTo(mesh.EdgeCount));
        Assert.That(mesh.Triangles, Is.Not.Empty);
        Assert.That(firstPath, Is.Not.Empty);
        Assert.That(secondPath, Is.Not.Empty);
    }

    [Test]
    public void NavigationMesh_BecomesStaleWhenContentMoves() {
        var world = CreateWorld();
        var content = new TerrainContent<ContentType>(ContentType.Tree, Rectangle(42, 35, 16, 30));
        world.AddContent(content);
        var mesh = world.BuildNavigationMesh(Settings());

        content.SetShape(Rectangle(60, 35, 16, 30));
        world.UpdateContent(content);

        Assert.That(mesh.IsCurrent, Is.False);
        Assert.Throws<InvalidOperationException>(() => mesh.FindPath(new LSVector2(15, 50), new LSVector2(85, 50)));
    }

    [Test]
    public void NavigationMesh_DynamicContentMovesWithoutInvalidatingStaticBake() {
        var world = CreateWorld();
        var content = new TerrainContent<ContentType>(ContentType.Tree, Rectangle(42, 35, 16, 30), TerrainContentMobility.Dynamic);
        world.AddContent(content);
        var mesh = world.BuildNavigationMesh(Settings());

        var firstPath = mesh.FindPath(new LSVector2(15, 50), new LSVector2(85, 50));
        content.SetShape(Rectangle(60, 35, 16, 30));
        world.UpdateContent(content);
        var secondPath = mesh.FindPath(new LSVector2(15, 50), new LSVector2(85, 50));

        Assert.That(mesh.IsCurrent, Is.True);
        Assert.That(mesh.BuildStatistics.ObstacleCount, Is.Zero);
        Assert.That(firstPath, Is.Not.Empty);
        Assert.That(secondPath, Is.Not.Empty);
        Assert.That(secondPath.Zip(secondPath.Skip(1), (from, to) => SegmentIntersectsRectangle(from, to, 60, 35, 16, 30)).All(intersects => !intersects), Is.True);
    }

    private static TerrainWorld<TerrainType, ContentType> CreateWorld() {
        var world = new TerrainWorld<TerrainType, ContentType>(new Bounds(50, 50, 100, 100), TerrainType.Grass);
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Grass, Rectangle(0, 0, 100, 100)));
        return world;
    }

    private static TerrainNavigationSettings<TerrainType, ContentType> Settings() {
        return new TerrainNavigationSettings<TerrainType, ContentType>(
            patch => patch?.Type switch {
                TerrainType.Water => 0f,
                TerrainType.Mud => 8f,
                _ => 1f,
            },
            agentRadius: 4f);
    }

    private static Polygon2D Rectangle(float x, float y, float width, float height) {
        return new Polygon2D(new[] {
            new LSVector2(x, y), new LSVector2(x + width, y),
            new LSVector2(x + width, y + height), new LSVector2(x, y + height),
        });
    }

    private static bool SegmentIntersectsRectangle(LSVector2 from, LSVector2 to, float x, float y, float width, float height) {
        var obstacle = Rectangle(x, y, width, height);
        for (int index = 0; index < obstacle.Vertices.Count; index++) {
            var a = obstacle.Vertices[index];
            var b = obstacle.Vertices[(index + 1) % obstacle.Vertices.Count];
            if (SegmentsIntersect(from, to, a, b)) return true;
        }
        return false;
    }

    private static void AssertTriangleDoesNotCrossBoundary(TerrainNavigationTriangle triangle, Polygon2D boundary) {
        var triangleEdges = new[] { (triangle.A, triangle.B), (triangle.B, triangle.C), (triangle.C, triangle.A) };
        foreach (var edge in triangleEdges) {
            for (int index = 0; index < boundary.Vertices.Count; index++) {
                var from = boundary.Vertices[index];
                var to = boundary.Vertices[(index + 1) % boundary.Vertices.Count];
                Assert.That(SegmentsProperlyIntersect(edge.Item1, edge.Item2, from, to), Is.False,
                    $"Triangle edge {edge.Item1}-{edge.Item2} crossed patch boundary {from}-{to}.");
            }
        }
    }

    private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        float Cross(LSVector2 p, LSVector2 q) => p.X * q.Y - p.Y * q.X;
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        return ((abC > 0f && abD < 0f) || (abC < 0f && abD > 0f))
            && ((cdA > 0f && cdB < 0f) || (cdA < 0f && cdB > 0f));
    }

    private static bool SegmentsProperlyIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        const float epsilon = 0.0001f;
        float Cross(LSVector2 p, LSVector2 q) => p.X * q.Y - p.Y * q.X;
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        return ((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon))
            && ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon));
    }

    private static bool IsNonDegenerate(TerrainNavigationTriangle triangle) {
        float area2 = MathF.Abs((triangle.B - triangle.A).Cross(triangle.C - triangle.A));
        return triangle.A.IsFinite() && triangle.B.IsFinite() && triangle.C.IsFinite() && area2 > 0.01f;
    }

    private static string DescribeDegenerateTriangles(System.Collections.Generic.IEnumerable<TerrainNavigationTriangle> triangles) {
        return string.Join(" | ", triangles.Where(triangle => !IsNonDegenerate(triangle)).Select(triangle => {
            float area2 = MathF.Abs((triangle.B - triangle.A).Cross(triangle.C - triangle.A));
            return $"{triangle.A}; {triangle.B}; {triangle.C}; area2={area2}";
        }));
    }

    private enum TerrainType { Grass, Water, Mud }
    private enum ContentType { Tree }
}
