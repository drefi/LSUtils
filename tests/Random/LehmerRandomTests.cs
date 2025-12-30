using System.Linq;
using NUnit.Framework;
using LehmerRandom = LSUtils.Lehmer.Random;

namespace LSUtils.Tests.Random;

[TestFixture]
public class LehmerRandomTests
{
    [Test]
    public void SameSeed_ShouldProduceSameSequence()
    {
        var r1 = new LehmerRandom(12345);
        var r2 = new LehmerRandom(12345);

        var seq1 = Enumerable.Range(0, 5).Select(_ => r1.Next()).ToArray();
        var seq2 = Enumerable.Range(0, 5).Select(_ => r2.Next()).ToArray();

        Assert.That(seq1, Is.EqualTo(seq2));
    }

    [Test]
    public void DifferentSeeds_ShouldProduceDifferentSequence()
    {
        var r1 = new LehmerRandom(1);
        var r2 = new LehmerRandom(2);

        var v1 = r1.Next();
        var v2 = r2.Next();

        Assert.That(v1, Is.Not.EqualTo(v2));
    }

    [Test]
    public void Next_WithMax_ShouldBeWithinRange()
    {
        var r = new LehmerRandom(42);
        for (int i = 0; i < 20; i++)
        {
            var v = r.Next(10);
            Assert.That(v, Is.GreaterThanOrEqualTo(0));
            Assert.That(v, Is.LessThan(10));
        }
    }

    [Test]
    public void Next_WithMinMax_ShouldBeWithinRange()
    {
        var r = new LehmerRandom(999);
        for (int i = 0; i < 20; i++)
        {
            var v = r.Next(-5, 5);
            Assert.That(v, Is.GreaterThanOrEqualTo(-5));
            Assert.That(v, Is.LessThan(5));
        }
    }

    [Test]
    public void NextDouble_ShouldBeWithinRange()
    {
        var r = new LehmerRandom(7);
        for (int i = 0; i < 20; i++)
        {
            var v = r.NextDouble(5.0);
            Assert.That(v, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(v, Is.LessThan(5.0));
        }
    }

    [Test]
    public void NextFloat_ShouldBeWithinRange()
    {
        var r = new LehmerRandom(77);
        for (int i = 0; i < 20; i++)
        {
            var v = r.NextFloat(-2.5f, 3.5f);
            Assert.That(v, Is.GreaterThanOrEqualTo(-2.5f));
            Assert.That(v, Is.LessThan(3.5f));
        }
    }

    [Test]
    public void NextBytes_ShouldFillBuffer()
    {
        var r = new LehmerRandom(5);
        var buffer = new byte[8];

        r.NextBytes(buffer);

        Assert.That(buffer.All(b => b == 0), Is.False);
    }
}
