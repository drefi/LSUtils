namespace LSUtils.Tests.Terrain;

using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Spatial;
using LSUtils.Terrain;

[TestFixture]
public class TerrainWorldTests {
    [Test]
    public void QueryPatchesAt_ReturnsAllContainingPatches() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);
        var sand = new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(0, 0, 20), layer: 0);
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(0, 0, 4), layer: 1);
        world.AddPatch(sand);
        world.AddPatch(water);

        var patches = world.QueryPatchesAt(0, 0);

        Assert.That(patches, Does.Contain(sand));
        Assert.That(patches, Does.Contain(water));
    }

    [Test]
    public void ResolveTerrainTypeAt_ReturnsHighestLayerPatch() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);
        world.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(0, 0, 20), layer: 0));
        world.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(0, 0, 4), layer: 1));

        var terrainType = world.ResolveTerrainTypeAt(0, 0);

        Assert.That(terrainType, Is.EqualTo(TestTerrainType.Water));
    }

    [Test]
    public void ResolveTerrainTypeAt_WhenNoPatchContainsPoint_ReturnsDefault() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);

        var terrainType = world.ResolveTerrainTypeAt(20, 20);

        Assert.That(terrainType, Is.EqualTo(TestTerrainType.Dry));
    }

    [Test]
    public void UpdatePatch_UpdatesSpatialIndexAfterGrowth() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(0, 0, 2));
        world.AddPatch(water);

        water.SetShape(Square(0, 0, 20));
        world.UpdatePatch(water);

        Assert.That(world.ResolveTerrainTypeAt(8, 0), Is.EqualTo(TestTerrainType.Water));
    }

    [Test]
    public void QueryContents_ReturnsContentsInArea() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);
        var tree = new TerrainContent<TestContentType>(TestContentType.Tree, Square(0, 0, 2));
        world.AddContent(tree);

        var contents = world.QueryContents(new Bounds(0, 0, 4, 4));

        Assert.That(contents, Does.Contain(tree));
    }

    private static Polygon2D Square(float x, float y, float size) {
        float half = size * 0.5f;
        return new Polygon2D(new[] {
            new LSVector2(x - half, y - half),
            new LSVector2(x + half, y - half),
            new LSVector2(x + half, y + half),
            new LSVector2(x - half, y + half),
        });
    }

    private enum TestTerrainType {
        Dry,
        Sand,
        Water,
    }

    private enum TestContentType {
        Tree,
    }
}
