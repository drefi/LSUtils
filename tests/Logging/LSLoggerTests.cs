using NUnit.Framework;
using LSUtils.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Tests.Logging;

/// <summary>
/// Test log provider that captures log entries for verification
/// </summary>
internal class TestLogProvider : ILSLogProvider
{
    public string Name => "Test";
    public bool IsAvailable { get; set; } = true;
    public List<LSLogEntry> CapturedEntries { get; } = new();

    public void WriteLog(LSLogEntry entry)
    {
        if (IsAvailable)
        {
            CapturedEntries.Add(entry);
        }
    }

    public void Clear() => CapturedEntries.Clear();
}

[TestFixture]
public class LSLoggerTests
{
    private TestLogProvider? _testProvider;
    private bool _originalEnabled;
    private LSLogLevel _originalMinLevel;

    [SetUp]
    public void SetUp()
    {
        _testProvider = new TestLogProvider();
        
        // Store original settings
        _originalEnabled = LSLogger.Singleton.IsEnabled;
        _originalMinLevel = LSLogger.Singleton.MinimumLevel;
        
        // Configure logger for testing
        LSLogger.Singleton.ClearProviders();
        LSLogger.Singleton.AddProvider(_testProvider);
        LSLogger.Singleton.IsEnabled = true;
        LSLogger.Singleton.MinimumLevel = LSLogLevel.DEBUG;
    }

    [TearDown]
    public void TearDown()
    {
        _testProvider?.Clear();
        
        // Restore original settings
        LSLogger.Singleton.ClearProviders();
        LSLogger.Singleton.AddProvider(new LSConsoleLogProvider());
        LSLogger.Singleton.IsEnabled = _originalEnabled;
        LSLogger.Singleton.MinimumLevel = _originalMinLevel;
        
        _testProvider = null;
    }

    [Test]
    public void Singleton_ShouldReturnSameInstance()
    {
        // Act
        var instance1 = LSLogger.Singleton;
        var instance2 = LSLogger.Singleton;

        // Assert
        Assert.That(instance1, Is.SameAs(instance2));
        Assert.That(instance1, Is.Not.Null);
    }

    [Test]
    public void IsEnabled_ShouldControlLogging()
    {
        // Arrange
        LSLogger.Singleton.IsEnabled = false;

        // Act
        LSLogger.Singleton.Info("Test message");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(0));
    }

    [Test]
    public void MinimumLevel_ShouldFilterLogs()
    {
        // Arrange
        LSLogger.Singleton.MinimumLevel = LSLogLevel.WARNING;

        // Act
        LSLogger.Singleton.Debug("Debug message");
        LSLogger.Singleton.Info("Info message");
        LSLogger.Singleton.Warning("Warning message");
        LSLogger.Singleton.Error("Error message");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(2));
        Assert.That(_testProvider.CapturedEntries.Any(e => e.Level == LSLogLevel.WARNING), Is.True);
        Assert.That(_testProvider.CapturedEntries.Any(e => e.Level == LSLogLevel.ERROR), Is.True);
    }

    [Test]
    public void Debug_ShouldLogDebugMessage()
    {
        // Act
        LSLogger.Singleton.Debug("Debug message", "TestSource");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Level, Is.EqualTo(LSLogLevel.DEBUG));
        Assert.That(entry.Message, Is.EqualTo("Debug message"));
        Assert.That(entry.Source, Is.EqualTo("TestSource"));
    }

    [Test]
    public void Info_ShouldLogInfoMessage()
    {
        // Act
        LSLogger.Singleton.Info("Info message", "TestSource");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Level, Is.EqualTo(LSLogLevel.INFO));
        Assert.That(entry.Message, Is.EqualTo("Info message"));
    }

    [Test]
    public void Warning_ShouldLogWarningMessage()
    {
        // Act
        LSLogger.Singleton.Warning("Warning message", "TestSource");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Level, Is.EqualTo(LSLogLevel.WARNING));
        Assert.That(entry.Message, Is.EqualTo("Warning message"));
    }

    [Test]
    public void Error_ShouldLogErrorMessage()
    {
        // Act
        LSLogger.Singleton.Error("Error message", null, "TestSource");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Level, Is.EqualTo(LSLogLevel.ERROR));
        Assert.That(entry.Message, Is.EqualTo("Error message"));
    }

    [Test]
    public void Critical_ShouldLogCriticalMessage()
    {
        // Act
        LSLogger.Singleton.Critical("Critical message", ("TestSource", null));

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Level, Is.EqualTo(LSLogLevel.CRITICAL));
        Assert.That(entry.Message, Is.EqualTo("Critical message"));
    }

    [Test]
    public void Log_WithProperties_ShouldIncludeProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            { "Key1", "Value1" },
            { "Key2", 42 }
        };

        // Act
        LSLogger.Singleton.Info("Test message", properties: properties);

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.Properties, Is.Not.Null);
        Assert.That(entry.Properties!.ContainsKey("Key1"), Is.True);
        Assert.That(entry.Properties["Key1"], Is.EqualTo("Value1"));
        Assert.That(entry.Properties["Key2"], Is.EqualTo(42));
    }

    [Test]
    public void Log_WithProcessId_ShouldIncludeProcessId()
    {
        // Arrange
        var processId = Guid.NewGuid();

        // Act
        LSLogger.Singleton.Info("Test message", processId: processId);

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        var entry = _testProvider.CapturedEntries.First();
        Assert.That(entry.ProcessId, Is.EqualTo(processId));
    }

    [Test]
    public void AddProvider_ShouldAddNewProvider()
    {
        // Arrange
        var secondProvider = new TestLogProvider();
        LSLogger.Singleton.AddProvider(secondProvider);

        // Act
        LSLogger.Singleton.Info("Test message");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(1));
        Assert.That(secondProvider.CapturedEntries.Count, Is.EqualTo(1));
    }

    [Test]
    public void DisableLogging_ShouldAddNullProvider()
    {
        // Act
        LSLogger.Singleton.DisableLogging();
        LSLogger.Singleton.Info("Test message");

        // Assert - Original provider should still have the message, but null provider discards
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(0));
    }

    [Test]
    public void ClearProviders_ShouldRemoveAllProviders()
    {
        // Act
        LSLogger.Singleton.ClearProviders();
        LSLogger.Singleton.Info("Test message");

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(0));
    }

    [Test]
    public void SetSourceStatus_ShouldControlSourceLogging()
    {
        // Arrange
        LSLogger.Singleton.SetSourceStatus(("TestSource", false));

        // Act
        LSLogger.Singleton.Info("Test message", ("TestSource", null));

        // Assert
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetSourceStatus_ShouldReturnSourceStatus()
    {
        // Arrange
        LSLogger.Singleton.SetSourceStatus(("TestSource", true));

        // Act
        var status = LSLogger.Singleton.GetSourceStatus("TestSource");

        // Assert
        Assert.That(status, Is.True);
    }

    [Test]
    public void IsSourceEnabled_ShouldReturnCorrectStatus()
    {
        // Arrange
        LSLogger.Singleton.SetSourceStatus(("EnabledSource", true));
        LSLogger.Singleton.SetSourceStatus(("DisabledSource", false));

        // Act & Assert
        Assert.That(LSLogger.Singleton.IsSourceEnabled("EnabledSource"), Is.True);
        Assert.That(LSLogger.Singleton.IsSourceEnabled("DisabledSource"), Is.False);
        Assert.That(LSLogger.Singleton.IsSourceEnabled("UnknownSource"), Is.True); // Default is enabled
    }

    [Test]
    public void SafeExecute_ShouldCatchAndLogExceptions()
    {
        // Arrange
        var exceptionThrown = false;

        // Act
        LSLogger.Singleton.SafeExecute(() =>
        {
            exceptionThrown = true;
            throw new LSException("Test exception");
        }, ("TestSource", null), "Test context");

        // Assert
        Assert.That(exceptionThrown, Is.True);
        Assert.That(_testProvider!.CapturedEntries.Any(e => e.Level == LSLogLevel.CRITICAL), Is.True);
    }

    [Test]
    public void SafeExecute_WithFunction_ShouldReturnDefaultOnException()
    {
        // Act
        var result = LSLogger.Singleton.SafeExecute(() =>
        {
            throw new LSException("Test exception");
#pragma warning disable CS0162 // Unreachable code detected
            return 42;
#pragma warning restore CS0162 // Unreachable code detected
        }, defaultValue: -1, source: ("TestSource", null), context: "Test context");

        // Assert
        Assert.That(result, Is.EqualTo(-1));
        Assert.That(_testProvider!.CapturedEntries.Any(e => e.Level == LSLogLevel.CRITICAL), Is.True);
    }

    [Test]
    public void SafeExecute_WithValidFunction_ShouldReturnResult()
    {
        // Act
        var result = LSLogger.Singleton.SafeExecute(() => 42, defaultValue: -1, source: ("TestSource", null));

        // Assert
        Assert.That(result, Is.EqualTo(42));
        Assert.That(_testProvider!.CapturedEntries.Count, Is.EqualTo(0));
    }
}
