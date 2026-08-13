namespace LSUtils.Tests.Terrain;

using System.Linq;
using NUnit.Framework;
using LSUtils.Geometry;
using LSUtils.Graphs.Algorithms;
using LSUtils.Spatial;
using LSUtils.Terrain;

[TestFixture]
public class TerrainRelationGraphBuilderTests {
    [Test]
    public void Build_AdjacentPatches_AddsAdjacencyRelation() {
        var grass = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var sand = new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(4, 0, 4));

        var graph = TerrainRelationGraphBuilder.Build(new[] { grass, sand });

        Assert.That(graph.GetRelations(grass, sand).Single().Relation, Is.EqualTo(TerrainRelationType.Adjacent));
        Assert.That(graph.GetRelations(sand, grass).Single().Relation, Is.EqualTo(TerrainRelationType.Adjacent));
    }

    [Test]
    public void Build_OverlappingPatches_AddsOverlapRelation() {
        var grass = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var mud = new TerrainPatch<TestTerrainType>(TestTerrainType.Mud, Square(1, 0, 4));

        var graph = TerrainRelationGraphBuilder.Build(new[] { grass, mud });

        Assert.That(graph.GetRelations(grass, mud).Single().Relation, Is.EqualTo(TerrainRelationType.Overlapping));
        Assert.That(graph.GetRelations(mud, grass).Single().Relation, Is.EqualTo(TerrainRelationType.Overlapping));
    }

    [Test]
    public void Build_ContainedPatch_AddsContainmentRelations() {
        var sand = new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(0, 0, 20));
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(0, 0, 4));

        var graph = TerrainRelationGraphBuilder.Build(new[] { sand, water });

        Assert.That(graph.GetRelations(sand, water).Single().Relation, Is.EqualTo(TerrainRelationType.Contains));
        Assert.That(graph.GetRelations(water, sand).Single().Relation, Is.EqualTo(TerrainRelationType.ContainedBy));
    }

    [Test]
    public void Build_DisjointPatches_AddsNoRelation() {
        var grass = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(20, 0, 4));

        var graph = TerrainRelationGraphBuilder.Build(new[] { grass, water });

        Assert.That(graph.GetRelations(grass, water), Is.Empty);
        Assert.That(graph.GetRelations(water, grass), Is.Empty);
    }

    [Test]
    public void Build_FromWorld_UsesSpatialQueriesAndAddsNodes() {
        var world = new TerrainWorld<TestTerrainType, TestContentType>(new Bounds(0, 0, 100, 100), TestTerrainType.Dry);
        var grass = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var sand = new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(4, 0, 4));
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(40, 0, 4));
        world.AddPatch(grass);
        world.AddPatch(sand);
        world.AddPatch(water);

        var graph = TerrainRelationGraphBuilder.Build(world);

        Assert.That(graph.HasNode(grass), Is.True);
        Assert.That(graph.HasNode(sand), Is.True);
        Assert.That(graph.HasNode(water), Is.True);
        Assert.That(graph.GetRelations(grass, sand).Single().Relation, Is.EqualTo(TerrainRelationType.Adjacent));
        Assert.That(graph.GetRelations(grass, water), Is.Empty);
    }

    [Test]
    public void RelationGraph_CanBeUsedForConnectedComponents() {
        var grass = new TerrainPatch<TestTerrainType>(TestTerrainType.Grass, Square(0, 0, 4));
        var sand = new TerrainPatch<TestTerrainType>(TestTerrainType.Sand, Square(4, 0, 4));
        var water = new TerrainPatch<TestTerrainType>(TestTerrainType.Water, Square(20, 0, 4));

        var graph = TerrainRelationGraphBuilder.Build(new[] { grass, sand, water });
        var components = GraphAlgorithms.ConnectedComponents(graph);

        Assert.That(components, Has.Count.EqualTo(2));
        Assert.That(components.Any(c => c.ToHashSet().SetEquals(new[] { grass, sand })), Is.True);
        Assert.That(components.Any(c => c.ToHashSet().SetEquals(new[] { water })), Is.True);
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
        Grass,
        Mud,
        Sand,
        Water,
    }

    private enum TestContentType {
        Tree,
    }
}
