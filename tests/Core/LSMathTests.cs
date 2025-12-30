using NUnit.Framework;

namespace LSUtils.Tests.Core;

[TestFixture]
public class LSMathTests
{
    [Test]
    public void Clamp_Int_ShouldRespectBounds()
    {
        Assert.That(LSMath.Clamp(5, 0, 10), Is.EqualTo(5));
        Assert.That(LSMath.Clamp(-1, 0, 10), Is.EqualTo(0));
        Assert.That(LSMath.Clamp(11, 0, 10), Is.EqualTo(10));
    }

    [Test]
    public void Clamp_Generic_ShouldRespectBounds()
    {
        Assert.That(LSMath.Clamp('c', 'a', 'f'), Is.EqualTo('c'));
        Assert.That(LSMath.Clamp('a', 'b', 'z'), Is.EqualTo('b'));
        Assert.That(LSMath.Clamp('z', 'b', 'y'), Is.EqualTo('y'));
    }

    [Test]
    public void MinMax_ShouldReturnCorrectValues()
    {
        Assert.That(LSMath.Min(2, 5), Is.EqualTo(2));
        Assert.That(LSMath.Max(2, 5), Is.EqualTo(5));
        Assert.That(LSMath.Min(2.5f, 1.5f), Is.EqualTo(1.5f));
        Assert.That(LSMath.Max(2.5f, 1.5f), Is.EqualTo(2.5f));
    }

    [Test]
    public void FloorCeil_ShouldMatchSystem()
    {
        Assert.That(LSMath.Floor(1.9f), Is.EqualTo(System.MathF.Floor(1.9f)));
        Assert.That(LSMath.Floor(1.9), Is.EqualTo(System.Math.Floor(1.9)));
        Assert.That(LSMath.Ceil(1.1f), Is.EqualTo(System.MathF.Ceiling(1.1f)));
        Assert.That(LSMath.Ceil(1.1), Is.EqualTo(System.Math.Ceiling(1.1)));
    }

    [Test]
    public void ParseFloat_ShouldReturnDefaultWhenInvalid()
    {
        Assert.That(LSMath.TryParseFloat("1.5", out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(1.5f));
        Assert.That(LSMath.ParseFloat("invalid", 3.14f), Is.EqualTo(3.14f));
    }

    [Test]
    public void TryParseInt_ShouldRespectCultureInvariant()
    {
        Assert.That(LSMath.TryParseInt("42", out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(42));
        Assert.That(LSMath.TryParseInt("not-int", out _), Is.False);
    }
}
