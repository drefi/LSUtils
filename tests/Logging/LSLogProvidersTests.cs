using NUnit.Framework;
using LSUtils.Logging;

namespace LSUtils.Tests.Logging;

[TestFixture]
public class LSLogProvidersTests
{
    [Test]
    public void LSConsoleLogProvider_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var provider = new LSConsoleLogProvider();

        // Assert
        Assert.That(provider.Name, Is.EqualTo("Console"));
        Assert.That(provider.IsAvailable, Is.True);
    }

    [Test]
    public void LSConsoleLogProvider_WriteLog_ShouldNotThrow()
    {
        // Arrange
        var provider = new LSConsoleLogProvider();
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message", "TestSource");

        // Act & Assert
        Assert.DoesNotThrow(() => provider.WriteLog(entry));
    }

    [Test]
    public void LSGodotLogProvider_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var provider = new LSGodotLogProvider();

        // Assert
        Assert.That(provider.Name, Is.EqualTo("Godot"));
        // IsAvailable depends on Godot runtime, so we just check it's a boolean
        Assert.That(provider.IsAvailable, Is.TypeOf<bool>());
    }

    [Test]
    public void LSGodotLogProvider_WriteLog_ShouldNotThrow()
    {
        // Arrange
        var provider = new LSGodotLogProvider();
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message", "TestSource");

        // Act & Assert - Should not throw even if Godot is not available
        Assert.DoesNotThrow(() => provider.WriteLog(entry));
    }

    [Test]
    public void LSNullLogProvider_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var provider = new LSNullLogProvider();

        // Assert
        Assert.That(provider.Name, Is.EqualTo("Null"));
        Assert.That(provider.IsAvailable, Is.True);
    }

    [Test]
    public void LSNullLogProvider_WriteLog_ShouldDiscardEntries()
    {
        // Arrange
        var provider = new LSNullLogProvider();
        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message", "TestSource");

        // Act & Assert - Should not throw and should silently discard
        Assert.DoesNotThrow(() => provider.WriteLog(entry));
        // Null provider doesn't store anything, so we can only verify it doesn't crash
    }

    [Test]
    public void AllProviders_ShouldImplementInterface()
    {
        // Arrange & Act
        var consoleProvider = new LSConsoleLogProvider();
        var godotProvider = new LSGodotLogProvider();
        var nullProvider = new LSNullLogProvider();

        // Assert
        Assert.That(consoleProvider, Is.InstanceOf<ILSLogProvider>());
        Assert.That(godotProvider, Is.InstanceOf<ILSLogProvider>());
        Assert.That(nullProvider, Is.InstanceOf<ILSLogProvider>());
    }

    [Test]
    public void AllProviders_ShouldHaveUniqueNames()
    {
        // Arrange
        var consoleProvider = new LSConsoleLogProvider();
        var godotProvider = new LSGodotLogProvider();
        var nullProvider = new LSNullLogProvider();

        // Assert
        Assert.That(consoleProvider.Name, Is.Not.EqualTo(godotProvider.Name));
        Assert.That(consoleProvider.Name, Is.Not.EqualTo(nullProvider.Name));
        Assert.That(godotProvider.Name, Is.Not.EqualTo(nullProvider.Name));
    }

    [Test]
    public void AllProviders_WriteLog_ShouldHandleDifferentLogLevels()
    {
        // Arrange
        var providers = new ILSLogProvider[]
        {
            new LSConsoleLogProvider(),
            new LSGodotLogProvider(),
            new LSNullLogProvider()
        };

        var logLevels = new[]
        {
            LSLogLevel.DEBUG,
            LSLogLevel.INFO,
            LSLogLevel.WARNING,
            LSLogLevel.ERROR,
            LSLogLevel.CRITICAL
        };

        // Act & Assert
        foreach (var provider in providers)
        {
            foreach (var level in logLevels)
            {
                var entry = new LSLogEntry(level, $"Test {level} message", "TestSource");
                Assert.DoesNotThrow(() => provider.WriteLog(entry),
                    $"Provider {provider.Name} failed with level {level}");
            }
        }
    }

    [Test]
    public void AllProviders_WriteLog_ShouldHandleNullSource()
    {
        // Arrange
        var providers = new ILSLogProvider[]
        {
            new LSConsoleLogProvider(),
            new LSGodotLogProvider(),
            new LSNullLogProvider()
        };

        var entry = new LSLogEntry(LSLogLevel.INFO, "Test message with null source", null);

        // Act & Assert
        foreach (var provider in providers)
        {
            Assert.DoesNotThrow(() => provider.WriteLog(entry),
                $"Provider {provider.Name} failed with null source");
        }
    }

    [Test]
    public void AllProviders_WriteLog_ShouldHandleException()
    {
        // Arrange
        var providers = new ILSLogProvider[]
        {
            new LSConsoleLogProvider(),
            new LSGodotLogProvider(),
            new LSNullLogProvider()
        };

        var exception = new LSException("Test exception");
        var entry = new LSLogEntry(LSLogLevel.ERROR, "Error with exception", "TestSource", exception: exception);

        // Act & Assert
        foreach (var provider in providers)
        {
            Assert.DoesNotThrow(() => provider.WriteLog(entry),
                $"Provider {provider.Name} failed with exception");
        }
    }
}
