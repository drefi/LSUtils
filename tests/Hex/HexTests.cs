using NUnit.Framework;
using LSUtils.Hex;
using HexCoord = LSUtils.Hex.Hex;

namespace LSUtils.Tests.Hex;

[TestFixture]
public class HexTests
{
    [Test]
    public void Equality_ShouldMatchCoordinates()
    {
        var a = new HexCoord(1, -2);
        var b = new HexCoord(1, -2);
        var c = new HexCoord(0, 0);

        Assert.That(a == b, Is.True);
        Assert.That(a != c, Is.True);
        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void AddSubtract_ShouldWork()
    {
        var a = new HexCoord(1, 0);
        var b = new HexCoord(0, 1);

        var sum = a.Add(b);
        var diff = sum.Subtract(a);

        Assert.That(sum, Is.EqualTo(new HexCoord(1, 1)));
        Assert.That(diff, Is.EqualTo(b));
    }

    [Test]
    public void Distance_ShouldMatchLength()
    {
        var origin = new HexCoord(0, 0);
        var target = new HexCoord(2, -1);

        var length = target.Length();
        var dist = origin.Distance(target);

        Assert.That(length, Is.EqualTo(dist));
        Assert.That(dist, Is.GreaterThan(0));
    }
}
