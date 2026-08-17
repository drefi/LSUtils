namespace LSUtils.Tests.Terrain;

using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Spatial;
using LSUtils.Terrain;

[TestFixture]
public class TerrainRegionTests {
    [Test]
    public void AddPatch_AllowsPatchToBeSharedAcrossRegions() {
        var patch = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var regionA = new TerrainRegion<TestTerrainType, TestContentType>();
        var regionB = new TerrainRegion<TestTerrainType, TestContentType>();

        regionA.AddPatch(patch);
        regionB.AddPatch(patch);

        Assert.That(regionA.Patches, Does.Contain(patch));
        Assert.That(regionB.Patches, Does.Contain(patch));
    }

    [Test]
    public void RecalculateBounds_CombinesPatchAndContentBounds() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4)));
        region.AddContent(new TerrainContent<TestContentType>(TestContentType.Tree, Square(10, 0, 2)));

        Assert.That(region.Bounds, Is.EqualTo(new Bounds(4.5f, 0, 13, 4)));
    }

    [Test]
    public void AddChild_SetsParent() {
        var parent = new TerrainRegion<TestTerrainType, TestContentType>();
        var child = new TerrainRegion<TestTerrainType, TestContentType>();

        parent.AddChild(child);

        Assert.That(parent.Children, Does.Contain(child));
        Assert.That(child.Parent, Is.EqualTo(parent));
    }

    [Test]
    public void AddChild_WithSelf_Throws() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();

        Assert.Throws<LSArgumentException>(() => region.AddChild(region));
    }

    [Test]
    public void AddChild_WithAncestor_ThrowsInsteadOfCreatingCycle() {
        var parent = new TerrainRegion<TestTerrainType, TestContentType>();
        var child = new TerrainRegion<TestTerrainType, TestContentType>();
        parent.AddChild(child);

        Assert.Throws<LSArgumentException>(() => child.AddChild(parent));
    }

    [Test]
    public void AddChild_ReparentsFromPreviousParent() {
        var firstParent = new TerrainRegion<TestTerrainType, TestContentType>();
        var secondParent = new TerrainRegion<TestTerrainType, TestContentType>();
        var child = new TerrainRegion<TestTerrainType, TestContentType>();
        firstParent.AddChild(child);

        secondParent.AddChild(child);

        Assert.That(firstParent.Children, Does.Not.Contain(child));
        Assert.That(secondParent.Children, Does.Contain(child));
        Assert.That(child.Parent, Is.SameAs(secondParent));
    }

    [Test]
    public void MemberShapeChange_AutomaticallyRecalculatesBounds() {
        var patch = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 2));
        var region = new TerrainRegion<TestTerrainType, TestContentType>(new[] { patch });

        patch.SetShape(Square(10, 0, 4));

        Assert.That(region.Bounds, Is.EqualTo(new Bounds(10, 0, 4, 4)));
    }

    [Test]
    public void PolygonCoverageArea_CountsOverlapOnce() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4)));
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(1, 1, 4)));

        Assert.That(region.MembershipArea, Is.EqualTo(32f));
        Assert.That(region.PolygonCoverageArea, Is.EqualTo(23f).Within(0.001f));
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
    }

    private enum TestContentType {
        Tree,
    }
}
