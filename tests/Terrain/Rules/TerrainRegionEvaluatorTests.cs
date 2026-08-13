namespace LSUtils.Tests.Terrain.Rules;

using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Terrain;
using LSUtils.Terrain.Rules;

[TestFixture]
public class TerrainRegionEvaluatorTests {
    [Test]
    public void EvaluationContext_ReturnsPatchAreaRatio() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4)));
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(10, 0, 4)));
        var context = new TerrainRegionEvaluationContext<TestTerrainType, TestContentType>(region);

        Assert.That(context.GetPatchAreaRatio(TestTerrainType.Grass), Is.EqualTo(0.5f));
    }

    [Test]
    public void EvaluationContext_ReturnsContentMetrics() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 10)));
        region.AddContent(new TerrainContent<TestContentType>(TestContentType.Tree, Square(0, 0, 2)));
        var context = new TerrainRegionEvaluationContext<TestTerrainType, TestContentType>(region);

        Assert.That(context.GetContentCount(TestContentType.Tree), Is.EqualTo(1));
        Assert.That(context.GetContentAreaRatio(TestContentType.Tree), Is.EqualTo(0.04f));
    }

    [Test]
    public void Evaluate_NoMatchingRule_ReturnsDefault() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();

        var result = TerrainRegionEvaluator.Evaluate(
            region,
            new ITerrainRegionRule<TestBiomeType, TestTerrainType, TestContentType>[] {
                new GrasslandRule(),
            },
            TestBiomeType.Plain);

        Assert.That(result, Is.EqualTo(TestBiomeType.Plain));
    }

    [Test]
    public void Evaluate_AllGrass_ReturnsGrassland() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4)));

        var result = TerrainRegionEvaluator.Evaluate(
            region,
            new ITerrainRegionRule<TestBiomeType, TestTerrainType, TestContentType>[] {
                new GrasslandRule(),
            },
            TestBiomeType.Plain);

        Assert.That(result, Is.EqualTo(TestBiomeType.Grassland));
    }

    [Test]
    public void Evaluate_HigherPriorityRuleWins() {
        var region = new TerrainRegion<TestTerrainType, TestContentType>();
        region.AddPatch(new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 10)));
        region.AddContent(new TerrainContent<TestContentType>(TestContentType.Tree, Square(0, 0, 9)));

        var result = TerrainRegionEvaluator.Evaluate(
            region,
            new ITerrainRegionRule<TestBiomeType, TestTerrainType, TestContentType>[] {
                new GrasslandRule(),
                new ForestRule(),
            },
            TestBiomeType.Plain);

        Assert.That(result, Is.EqualTo(TestBiomeType.Forest));
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

    private sealed class GrasslandRule : ITerrainRegionRule<TestBiomeType, TestTerrainType, TestContentType> {
        public int Priority => 1;
        public TestBiomeType Result => TestBiomeType.Grassland;
        public bool Matches(TerrainRegionEvaluationContext<TestTerrainType, TestContentType> context) {
            return context.GetPatchAreaRatio(TestTerrainType.Grass) >= 0.66f;
        }
    }

    private sealed class ForestRule : ITerrainRegionRule<TestBiomeType, TestTerrainType, TestContentType> {
        public int Priority => 2;
        public TestBiomeType Result => TestBiomeType.Forest;
        public bool Matches(TerrainRegionEvaluationContext<TestTerrainType, TestContentType> context) {
            return context.GetContentAreaRatio(TestContentType.Tree) >= 0.8f;
        }
    }

    private enum TestTerrainType {
        Grass,
        Sand,
    }

    private enum TestContentType {
        Tree,
    }

    private enum TestBiomeType {
        Plain,
        Grassland,
        Forest,
    }
}
