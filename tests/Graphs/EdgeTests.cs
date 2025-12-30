namespace LSUtils.Tests.Graphs;

using NUnit.Framework;
using LSUtils.Graphs;

[TestFixture]
public class EdgeTests {
    [Test]
    public void Edge_ShouldStoreFromToAndCost() {
        // Arrange & Act
        var edge = new Edge<string>("A", "B", 5.0f);

        // Assert
        Assert.That(edge.From, Is.EqualTo("A"));
        Assert.That(edge.To, Is.EqualTo("B"));
        Assert.That(edge.Cost, Is.EqualTo(5.0f));
    }

    [Test]
    public void Edge_ShouldHaveDefaultCostOfOne() {
        // Arrange & Act
        var edge = new Edge<string>("A", "B");

        // Assert
        Assert.That(edge.Cost, Is.EqualTo(1.0f));
    }

    [Test]
    public void Edge_ShouldSupportValueEquality() {
        // Arrange
        var edge1 = new Edge<string>("A", "B", 3.0f);
        var edge2 = new Edge<string>("A", "B", 3.0f);
        var edge3 = new Edge<string>("A", "C", 3.0f);

        // Assert
        Assert.That(edge1, Is.EqualTo(edge2));
        Assert.That(edge1, Is.Not.EqualTo(edge3));
    }

    [Test]
    public void Edge_ShouldWorkWithDifferentNodeTypes() {
        // Arrange & Act
        var intEdge = new Edge<int>(1, 2, 10.5f);
        var stringEdge = new Edge<string>("start", "end");

        // Assert
        Assert.That(intEdge.From, Is.EqualTo(1));
        Assert.That(intEdge.To, Is.EqualTo(2));
        Assert.That(stringEdge.From, Is.EqualTo("start"));
        Assert.That(stringEdge.To, Is.EqualTo("end"));
    }
}
