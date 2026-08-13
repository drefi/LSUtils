namespace LSUtils.Tests.Graphs;

using System.Linq;
using NUnit.Framework;
using LSUtils.Graphs;

[TestFixture]
public class RelationGraphTests {
    [Test]
    public void AddRelation_ShouldAddNodesAndTypedRelation() {
        var graph = new RelationGraph<string, TestRelation>();

        graph.AddRelation("sand", "water", TestRelation.Overlaps, 2f);

        Assert.That(graph.HasNode("sand"), Is.True);
        Assert.That(graph.HasNode("water"), Is.True);
        Assert.That(graph.GetNeighbors("sand"), Does.Contain("water"));

        var relation = graph.GetRelations("sand", "water").Single();
        Assert.That(relation.Relation, Is.EqualTo(TestRelation.Overlaps));
        Assert.That(relation.Weight, Is.EqualTo(2f));
    }

    [Test]
    public void AddUndirectedRelation_ShouldAddBothDirections() {
        var graph = new RelationGraph<string, TestRelation>();

        graph.AddUndirectedRelation("grass", "sand", TestRelation.Adjacent);

        Assert.That(graph.GetNeighbors("grass"), Does.Contain("sand"));
        Assert.That(graph.GetNeighbors("sand"), Does.Contain("grass"));
    }

    [Test]
    public void RemoveRelations_ShouldRemoveOnlyRequestedDirection() {
        var graph = new RelationGraph<string, TestRelation>();
        graph.AddUndirectedRelation("grass", "sand", TestRelation.Adjacent);

        var removed = graph.RemoveRelations("grass", "sand");

        Assert.That(removed, Is.True);
        Assert.That(graph.GetNeighbors("grass"), Does.Not.Contain("sand"));
        Assert.That(graph.GetNeighbors("sand"), Does.Contain("grass"));
    }

    [Test]
    public void RemoveNode_ShouldRemoveIncomingRelations() {
        var graph = new RelationGraph<string, TestRelation>();
        graph.AddRelation("sand", "water", TestRelation.Contains);
        graph.AddRelation("grass", "water", TestRelation.Adjacent);

        graph.RemoveNode("water");

        Assert.That(graph.HasNode("water"), Is.False);
        Assert.That(graph.GetNeighbors("sand"), Does.Not.Contain("water"));
        Assert.That(graph.GetNeighbors("grass"), Does.Not.Contain("water"));
    }

    private enum TestRelation {
        Adjacent,
        Overlaps,
        Contains,
    }
}
