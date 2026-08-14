namespace LSUtils.Tests.Terrain;

using NUnit.Framework;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Spatial;
using LSUtils.Terrain;
using LSUtils.Terrain.Navigation;

[TestFixture]
public class TerrainPathfinderTests {
    [Test]
    public void FindPath_AvoidsImpassableWaterWithClearance() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Water, Rectangle(40, 20, 20, 60), layer: 1));

        var path = world.FindPath(new LSVector2(20, 50), new LSVector2(80, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Any(point => point.Y < 15f || point.Y > 85f), Is.True);
        Assert.That(path.All(point => !Rectangle(40, 20, 20, 60).Contains(point.X, point.Y)), Is.True);
    }

    [Test]
    public void FindPath_ChoosesLowerTerrainCostWhenAlternativeExists() {
        var world = CreateWorld();
        world.AddPatch(new TerrainPatch<TerrainType>(TerrainType.Mud, Rectangle(25, 35, 50, 30), layer: 1));

        var path = world.FindPath(new LSVector2(10, 50), new LSVector2(90, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Any(point => point.Y < 35f || point.Y > 65f), Is.True);
    }

    [Test]
    public void FindPath_AvoidsBlockingContent() {
        var world = CreateWorld();
        world.AddContent(new TerrainContent<ContentType>(ContentType.Tree, Rectangle(42, 35, 16, 30)));

        var path = world.FindPath(new LSVector2(15, 50), new LSVector2(85, 50), Settings());

        Assert.That(path, Is.Not.Empty);
        Assert.That(path.Zip(path.Skip(1), (from, to) => SegmentIntersectsRectangle(from, to, 42, 35, 16, 30)).All(intersects => !intersects), Is.True);
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

    private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        float Cross(LSVector2 p, LSVector2 q) => p.X * q.Y - p.Y * q.X;
        float abC = Cross(b - a, c - a);
        float abD = Cross(b - a, d - a);
        float cdA = Cross(d - c, a - c);
        float cdB = Cross(d - c, b - c);
        return ((abC > 0f && abD < 0f) || (abC < 0f && abD > 0f))
            && ((cdA > 0f && cdB < 0f) || (cdA < 0f && cdB > 0f));
    }

    private enum TerrainType { Grass, Water, Mud }
    private enum ContentType { Tree }
}
