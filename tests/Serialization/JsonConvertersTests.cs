using System;
using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;

namespace LSUtils.Tests.Serialization;

[TestFixture]
public class JsonConvertersTests
{
    private JsonSerializerOptions _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new InvariantCultureIntConverter());
        _options.Converters.Add(new InvariantCultureFloatConverter());
        _options.Converters.Add(new InvariantCultureDoubleConverter());
        _options.Converters.Add(new InvariantCultureLongConverter());
        _options.Converters.Add(new SystemGuidConverter());
        _options.Converters.Add(new LSSerializerInfoConverter());
        _options.Converters.Add(new LSSerializerInfoListConverter());
    }

    [Test]
    public void InvariantInt_ShouldReadStringAndWriteString()
    {
        var value = JsonSerializer.Deserialize<int>("\"123\"", _options);
        Assert.That(value, Is.EqualTo(123));

        var json = JsonSerializer.Serialize(123, _options);
        Assert.That(json, Is.EqualTo("\"123\""));
    }

    [Test]
    public void InvariantFloat_ShouldReadStringAndWriteString()
    {
        var value = JsonSerializer.Deserialize<float>("\"3.14\"", _options);
        Assert.That(value, Is.EqualTo(3.14f));

        var json = JsonSerializer.Serialize(1.5f, _options);
        Assert.That(json, Is.EqualTo("\"1.5\""));
    }

    [Test]
    public void InvariantDouble_ShouldReadStringAndWriteString()
    {
        var value = JsonSerializer.Deserialize<double>("\"2.5\"", _options);
        Assert.That(value, Is.EqualTo(2.5d));

        var json = JsonSerializer.Serialize(2.75d, _options);
        Assert.That(json, Is.EqualTo("\"2.75\""));
    }

    [Test]
    public void InvariantLong_ShouldReadStringAndWriteString()
    {
        var value = JsonSerializer.Deserialize<long>("\"9223372036854775807\"", _options);
        Assert.That(value, Is.EqualTo(9223372036854775807L));

        var json = JsonSerializer.Serialize(42L, _options);
        Assert.That(json, Is.EqualTo("\"42\""));
    }

    [Test]
    public void GuidConverter_ShouldRoundtrip()
    {
        var guid = Guid.NewGuid();
        var json = JsonSerializer.Serialize(guid, _options);
        var roundtrip = JsonSerializer.Deserialize<Guid>(json, _options);

        Assert.That(roundtrip, Is.EqualTo(guid));
    }

    [Test]
    public void LSSerializerInfoConverter_ShouldRoundtrip()
    {
        var info = new LSSerializerInfo("key", "value", typeof(string).AssemblyQualifiedName ?? string.Empty);

        var json = JsonSerializer.Serialize(info, _options);
        var roundtrip = JsonSerializer.Deserialize<LSSerializerInfo>(json, _options);

        Assert.That(roundtrip.Key, Is.EqualTo(info.Key));
        Assert.That(roundtrip.Value, Is.EqualTo(info.Value));
        Assert.That(roundtrip.AssemblyName, Is.EqualTo(info.AssemblyName));
    }

    [Test]
    public void LSSerializerInfoListConverter_ShouldRoundtripList()
    {
        var list = new List<LSSerializerInfo>
        {
            new("a", "1", typeof(int).AssemblyQualifiedName ?? string.Empty),
            new("b", "2", typeof(string).AssemblyQualifiedName ?? string.Empty)
        };

        var json = JsonSerializer.Serialize(list, _options);
        var roundtrip = JsonSerializer.Deserialize<List<LSSerializerInfo>>(json, _options);

        Assert.That(roundtrip, Is.Not.Null);
        Assert.That(roundtrip!.Count, Is.EqualTo(2));
        Assert.That(roundtrip[0].Key, Is.EqualTo("a"));
        Assert.That(roundtrip[1].Value, Is.EqualTo("2"));
    }
}
