namespace LSUtils.Logging;

using System.Collections.Generic;
using System.Collections.Concurrent;
/// <summary>
/// Central logging system with configurable providers and filtering.
/// Thread-safe singleton that manages log output and provides structured logging capabilities.
/// </summary>
public sealed class LSLogger {
    private static readonly System.Lazy<LSLogger> _instance = new(() => new LSLogger());

    /// <summary>
    /// Gets the singleton instance of the event logger.
    /// </summary>
    public static LSLogger Singleton => _instance.Value;

    private readonly ConcurrentBag<ILSLogProvider> _providers = new();
    private volatile LSLogLevel _minimumLevel = LSLogLevel.INFO;
    private volatile bool _isEnabled = true;

    /// <summary>
    /// Initializes the logger with default providers.
    /// </summary>
    private LSLogger() {
        // Add default providers in order of preference
        if (new LSGodotLogProvider().IsAvailable) {
            _providers.Add(new LSGodotLogProvider());
        } else {
            _providers.Add(new LSConsoleLogProvider());
        }
    }

    /// <summary>
    /// Gets or sets the minimum log level that will be processed.
    /// Log entries below this level will be discarded for performance.
    /// </summary>
    public LSLogLevel MinimumLevel {
        get => _minimumLevel;
        set => _minimumLevel = value;
    }

    /// <summary>
    /// Gets or sets whether logging is enabled.
    /// When disabled, all log entries are discarded immediately.
    /// </summary>
    public bool IsEnabled {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Adds a log provider to the collection of output targets.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    public void AddProvider(ILSLogProvider provider) {
        if (provider != null) {
            _providers.Add(provider);
        }
    }

    /// <summary>
    /// Removes all providers and adds the null provider to disable logging.
    /// </summary>
    public void DisableLogging() {
        ClearProviders();
        _providers.Add(new LSNullLogProvider());
        _isEnabled = false;
    }

    /// <summary>
    /// Removes all current providers.
    /// </summary>
    public void ClearProviders() {
        while (_providers.TryTake(out _)) {
            // Remove all providers
        }
    }

    /// <summary>
    /// Logs a debug message with optional structured data.
    /// </summary>
    public void Debug(string message, string? source = null, System.Guid? processId = null,
                     IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.DEBUG, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs an info message with optional structured data.
    /// </summary>
    public void Info(string message, string? source = null, System.Guid? processId = null,
                    IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.INFO, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs a warning message with optional structured data.
    /// </summary>
    public void Warning(string message, string? source = null, System.Guid? processId = null,
                       IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.WARNING, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs an error message with optional exception and structured data.
    /// </summary>
    public void Error(string message, LSException? exception = null, string? source = null,
                     System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.ERROR, message, source, exception: exception, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs a critical error message with optional exception and structured data.
    /// </summary>
    public void Critical(string message, LSException? exception = null, string? source = null,
                        System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.CRITICAL, message, source, exception: exception, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs an exception as a critical error with full details.
    /// </summary>
    public void LogException(LSException exception, string message = "Unhandled exception occurred",
                           string? source = null, System.Guid? processId = null,
                           IDictionary<string, object>? properties = null) {
        Critical(message, exception, source, processId, properties);
    }

    /// <summary>
    /// Executes an action within a try-catch block and logs any exceptions as critical errors.
    /// </summary>
    public void SafeExecute(LSAction action, string context = "Unknown operation",
                          string? source = null, System.Guid? processId = null) {
        try {
            action?.Invoke();
        } catch (LSException ex) {
            Critical($"Exception in {context}", ex, source, processId);
        }
    }

    /// <summary>
    /// Executes a function within a try-catch block and logs any exceptions as critical errors.
    /// Returns the default value of T if an exception occurs.
    /// </summary>
    public T SafeExecute<T>(System.Func<T> func, T defaultValue = default!, string context = "Unknown operation",
                          string? source = null, System.Guid? processId = null) {
        try {
            return func != null ? func() : defaultValue;
        } catch (LSException ex) {
            Critical($"Exception in {context}", ex, source, processId);
            return defaultValue;
        }
    }

    /// <summary>
    /// Core logging method that handles level filtering and provider dispatch.
    /// </summary>
    private void Log(LSLogLevel level, string message, string? source, LSException? exception = null,
                    System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        if (!_isEnabled || level < _minimumLevel) {
            return;
        }

        try {
            var entry = new LSLogEntry(
                level,
                message,
                source,
                exception: exception,
                processId: processId,
                properties: properties?.AsReadOnlyDictionary()
            );

            foreach (var provider in _providers) {
                if (provider.IsAvailable) {
                    try {
                        provider.WriteLog(entry);
                    } catch {
                        // Silent failure per provider - logging should never break the application
                    }
                }
            }
        } catch {
            // Silent failure - logging should never break the application
        }
    }
}

/// <summary>
/// Extension methods for dictionary conversion.
/// </summary>
internal static class DictionaryExtensions {
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary) where TKey : notnull {
        return dictionary as IReadOnlyDictionary<TKey, TValue> ??
               new Dictionary<TKey, TValue>(dictionary);
    }
}
