namespace LSUtils.Tests.Terrain;

using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Terrain;

[TestFixture]
public class TerrainPatchTests {
    [Test]
    public void Constructor_ExposesShapeProperties() {
        var shape = Square(0, 0, 4);
        var patch = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, shape, layer: 1, priority: 2);

        Assert.That(patch.Type, Is.EqualTo(TestTerrainType.Grass));
        Assert.That(patch.Bounds, Is.EqualTo(shape.Bounds));
        Assert.That(patch.Area, Is.EqualTo(shape.Area));
        Assert.That(patch.Layer, Is.EqualTo(1));
        Assert.That(patch.Priority, Is.EqualTo(2));
    }

    [Test]
    public void SetShape_UpdatesBoundsAndArea() {
        var patch = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(0, 0, 2));

        patch.SetShape(Square(0, 0, 4));

        Assert.That(patch.Area, Is.EqualTo(16f));
        Assert.That(patch.Contains(1.5f, 1.5f), Is.True);
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
        Grass,
        Water,
    }
}
