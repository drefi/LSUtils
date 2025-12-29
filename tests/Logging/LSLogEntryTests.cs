using NUnit.Framework;
using LSUtils.Logging;
using System;
using System.Collections.Generic;

namespace LSUtils.Tests.Logging;

[TestFixture]
public class LSLogEntryTests
{
    [Test]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var level = LSLogLevel.INFO;
        var message = "Test message";
        var source = "TestSource";
        var timestamp = DateTime.UtcNow;
        var processId = Guid.NewGuid();
        var properties = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var entry = new LSLogEntry(level, message, source, timestamp, null, properties, processId);

        // Assert
        Assert.That(entry.Level, Is.EqualTo(level));
        Assert.That(entry.Message, Is.EqualTo(message));
        Assert.That(entry.Source, Is.EqualTo(source));
        Assert.That(entry.Timestamp, Is.EqualTo(timestamp));
        Assert.That(entry.ProcessId, Is.EqualTo(processId));
        Assert.That(entry.Properties, Is.EqualTo(properties));
        Assert.That(entry.Exception, Is.Null);
    }

    [Test]
    public void Constructor_WithNullMessage_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<LSArgumentNullException>(() =>
            new LSLogEntry(LSLogLevel.INFO, null!, "TestSource"));
    }

    [Test]
    public void Constructor_WithNullTimestamp_ShouldUseCurrentTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test", "TestSource");
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.That(entry.Timestamp, Is.GreaterThanOrEqualTo(beforeCreation));
        Assert.That(entry.Timestamp, Is.LessThanOrEqualTo(afterCreation));
    }

    [Test]
    public void Constructor_WithException_ShouldStoreException()
    {
        // Arrange
        var exception = new LSException("Test exception");

        // Act
        var entry = new LSLogEntry(LSLogLevel.ERROR, "Test", "TestSource", exception: exception);

        // Assert
        Assert.That(entry.Exception, Is.EqualTo(exception));
    }

    [Test]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message", "TestSource");

        // Act
        var result = entry.ToString();

        // Assert
        Assert.That(result, Contains.Substring("INFO"));
        Assert.That(result, Contains.Substring("Test message"));
        Assert.That(result, Contains.Substring("TestSource"));
    }

    [Test]
    public void ToString_WithNullSource_ShouldHandleGracefully()
    {
        // Arrange
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message", null);

        // Act
        var result = entry.ToString();

        // Assert
        Assert.That(result, Contains.Substring("INFO"));
        Assert.That(result, Contains.Substring("Test message"));
    }

    [Test]
    public void ToString_WithException_ShouldIncludeException()
    {
        // Arrange
        var exception = new LSException("Test exception");
        var entry = new LSLogEntry(LSLogLevel.ERROR, "Error occurred", "TestSource", exception: exception);

        // Act
        var result = entry.ToString();

        // Assert
        Assert.That(result, Contains.Substring("ERROR"));
        Assert.That(result, Contains.Substring("Error occurred"));
        Assert.That(result, Contains.Substring("Test exception"));
    }

    [Test]
    public void ToString_WithProperties_ShouldIncludeProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            { "UserId", 123 },
            { "Action", "Login" }
        };
        var entry = new LSLogEntry(LSLogLevel.INFO, "User action", "TestSource", properties: properties);

        // Act
        var result = entry.ToString();

        // Assert
        Assert.That(result, Contains.Substring("User action"));
        Assert.That(result, Contains.Substring("UserId"));
        Assert.That(result, Contains.Substring("123"));
    }

    [Test]
    public void SourcePadding_ShouldBeConfigurable()
    {
        // Arrange
        var originalPadding = LSLogEntry.SourcePadding;
        LSLogEntry.SourcePadding = 15;

        try
        {
            // Act
            var entry = new LSLogEntry(LSLogLevel.INFO, "Test", "Source");
            var result = entry.ToString();

            // Assert
            Assert.That(result, Is.Not.Null);
            // The padding should affect the formatting
            Assert.That(LSLogEntry.SourcePadding, Is.EqualTo(15));
        }
        finally
        {
            // Cleanup
            LSLogEntry.SourcePadding = originalPadding;
        }
    }

    [Test]
    public void LevelPadding_ShouldBeConfigurable()
    {
        // Arrange
        var originalPadding = LSLogEntry.LevelPadding;
        LSLogEntry.LevelPadding = 12;

        try
        {
            // Act
            var entry = new LSLogEntry(LSLogLevel.CRITICAL, "Test", "Source");
            var result = entry.ToString();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(LSLogEntry.LevelPadding, Is.EqualTo(12));
        }
        finally
        {
            // Cleanup
            LSLogEntry.LevelPadding = originalPadding;
        }
    }
}

[TestFixture]
public class LSLogLevelTests
{
    [Test]
    public void LSLogLevel_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.That((int)LSLogLevel.DEBUG, Is.EqualTo(0));
        Assert.That((int)LSLogLevel.INFO, Is.EqualTo(1));
        Assert.That((int)LSLogLevel.WARNING, Is.EqualTo(2));
        Assert.That((int)LSLogLevel.ERROR, Is.EqualTo(3));
        Assert.That((int)LSLogLevel.CRITICAL, Is.EqualTo(4));
    }

    [Test]
    public void LSLogLevel_ShouldHaveAllExpectedValues()
    {
        // Assert
        Assert.That(Enum.IsDefined(typeof(LSLogLevel), LSLogLevel.DEBUG), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSLogLevel), LSLogLevel.INFO), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSLogLevel), LSLogLevel.WARNING), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSLogLevel), LSLogLevel.ERROR), Is.True);
        Assert.That(Enum.IsDefined(typeof(LSLogLevel), LSLogLevel.CRITICAL), Is.True);
    }

    [Test]
    public void LSLogLevel_ShouldBeOrdered()
    {
        // Assert - Higher severity should have higher values
        Assert.That(LSLogLevel.DEBUG < LSLogLevel.INFO, Is.True);
        Assert.That(LSLogLevel.INFO < LSLogLevel.WARNING, Is.True);
        Assert.That(LSLogLevel.WARNING < LSLogLevel.ERROR, Is.True);
        Assert.That(LSLogLevel.ERROR < LSLogLevel.CRITICAL, Is.True);
    }
}
