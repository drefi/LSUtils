using NUnit.Framework;

namespace LSUtils.Tests.Core;

[TestFixture]
public class LSVersionTests
{
    [Test]
    public void TryParse_ShouldPopulateFields()
    {
        const string versionString = "1.2-20251230B3";
        var parsedOk = LSVersion.TryParse(versionString, out var version);

        Assert.That(parsedOk, Is.True);
        Assert.That(version.majorVersion, Is.EqualTo(1));
        Assert.That(version.minorVersion, Is.EqualTo(2));
        Assert.That(version.dateVersion, Is.EqualTo(20251230));
        Assert.That(version.buildVersionType, Is.EqualTo(BuildVersionType.BETA));
        Assert.That(version.buildVersion, Is.EqualTo(3));
        Assert.That(version.ToString(), Is.EqualTo(versionString));
    }

    [Test]
    public void CompareAndCompatibility_ShouldWork()
    {
        var a = new LSVersion { majorVersion = 1, minorVersion = 0, dateVersion = 20250101, buildVersion = 1, buildVersionType = BuildVersionType.ALPHA };
        var b = new LSVersion { majorVersion = 1, minorVersion = 0, dateVersion = 20250101, buildVersion = 1, buildVersionType = BuildVersionType.ALPHA };
        var c = new LSVersion { majorVersion = 1, minorVersion = 1, dateVersion = 20250102, buildVersion = 2, buildVersionType = BuildVersionType.BETA };

        Assert.That(a.Compare(b), Is.True);
        Assert.That(a.Compatible(b), Is.True);
        Assert.That(a.Older(c), Is.True);
        Assert.That(c.Newer(a), Is.True);
    }

    [Test]
    public void IncreaseMethods_ShouldRaiseOnChange()
    {
        var version = new LSVersion { majorVersion = 1, minorVersion = 1, dateVersion = 20250101, buildVersion = 1, buildVersionType = BuildVersionType.ALPHA };
        var changeCount = 0;
        version.OnChange += () => changeCount++;

        version.IncreaseBuild();
        version.IncreaseMinor();
        version.IncreaseMajor();

        // UpdateDate inside IncreaseMinor/IncreaseMajor can raise additional callbacks; ensure at least one per call
        Assert.That(changeCount, Is.GreaterThanOrEqualTo(3));
        Assert.That(version.majorVersion, Is.EqualTo(2));
        Assert.That(version.minorVersion, Is.EqualTo(1));
        Assert.That(version.buildVersion, Is.GreaterThanOrEqualTo(1));
    }
}
