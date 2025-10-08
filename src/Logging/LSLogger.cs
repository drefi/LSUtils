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
    ConcurrentDictionary<string, bool?> _sources = new();
    public bool? SetSource((string sourceID, bool? isEnabled)? source, bool dontUpdateSources = false) {
        // If source is null, return true (meaning no sourceID is enabled by default)
        if (source == null) return true;
        string sourceID = source.Value.sourceID;
        // source.isEnabled always overrides existing value if it has value
        bool? enabled = _sources.TryGetValue(sourceID, out enabled) ? enabled : null;
        var isEnabled = source.Value.isEnabled.HasValue ? source.Value.isEnabled : enabled;
        // If isEnabled has value, add or update the source
        if (!enabled.HasValue || (isEnabled.HasValue && !dontUpdateSources)) {
            _sources.AddOrUpdate(sourceID, isEnabled, (key, oldValue) => isEnabled);
        }
        return isEnabled;
    }
    public bool IsSourceEnabled(string sourceID) {
        if (string.IsNullOrWhiteSpace(sourceID)) return false;
        return _sources.TryGetValue(sourceID, out var isEnabled) ? isEnabled ?? false : false;
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
        (string, bool?)? tuple = source == null ? null : (source, null);
        Log(LSLogLevel.DEBUG, message, tuple, processId: processId, properties: properties);
    }
    public void Debug(string message, (string sourceID, bool? isEnabled)? source = null, System.Guid? processId = null,
                     IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.DEBUG, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs an info message with optional structured data.
    /// </summary>
    public void Info(string message, string? source = null, System.Guid? processId = null,
                    IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.INFO, message, source != null ? (source, null) : null, processId: processId, properties: properties);
    }
    public void Info(string message, (string sourceID, bool? isEnabled)? source = null, System.Guid? processId = null,
                    IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.INFO, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs a warning message with optional structured data.
    /// </summary>
    public void Warning(string message, string? source = null, System.Guid? processId = null,
                       IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.WARNING, message, source != null ? (source, null) : null, processId: processId, properties: properties);
    }
    public void Warning(string message, (string sourceID, bool? isEnabled)? source = null, System.Guid? processId = null,
                       IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.WARNING, message, source, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs an error message with optional exception and structured data.
    /// </summary>
    public void Error(string message, LSException? exception = null, string? source = null,
                     System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.ERROR, message, source != null ? (source, null) : null, exception: exception, processId: processId, properties: properties);
    }
    public void Error(string message, (string sourceID, bool? isEnabled)? source = null, LSException? exception = null,
                     System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.ERROR, message, source, exception: exception, processId: processId, properties: properties);
    }

    /// <summary>
    /// Logs a critical error message with optional exception and structured data.
    /// </summary>
    public void Critical(string message, LSException? exception = null, string? source = null,
                        System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        Log(LSLogLevel.CRITICAL, message, source != null ? (source, null) : null, exception: exception, processId: processId, properties: properties);
    }
    public void Critical(string message, (string sourceID, bool? isEnabled)? source = null, LSException? exception = null,
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
    public void LogException(LSException exception, string message = "Unhandled exception occurred",
                           (string sourceID, bool? isEnabled)? source = null, System.Guid? processId = null,
                           IDictionary<string, object>? properties = null) {
        Critical(message, source, exception, processId, properties);
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
    public void SafeExecute(LSAction action, (string sourceID, bool? isEnabled)? source = null,
                          string context = "Unknown operation", System.Guid? processId = null) {
        try {
            action?.Invoke();
        } catch (LSException ex) {
            Critical($"Exception in {context}", source, ex, processId);
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
    public T SafeExecute<T>(System.Func<T> func, T defaultValue = default!, (string sourceID, bool? isEnabled)? source = null,
                          string context = "Unknown operation", System.Guid? processId = null) {
        try {
            return func != null ? func() : defaultValue;
        } catch (LSException ex) {
            Critical($"Exception in {context}", source, ex, processId);
            return defaultValue;
        }
    }

    /// <summary>
    /// Core logging method that handles level filtering and provider dispatch.
    /// </summary>
    private void Log(LSLogLevel level, string message, (string sourceID, bool? isEnabled)? source, LSException? exception = null,
                    System.Guid? processId = null, IDictionary<string, object>? properties = null) {
        if (!_isEnabled || level < _minimumLevel) {
            return;
        }
        // source.IsEnabled == true => true
        // source.IsEnabled == false => false
        // source.IsEnabled == null => _sources[sourceID] or true if not found
        //  - _sources[sourceID] == null => true
        //  - _sources[sourceID] == true => true
        //  - _sources[sourceID] == false => false
        var isEnabled = SetSource(source, true); // dontUpdateSources = true to avoid modifying sources
        if (isEnabled.HasValue && !isEnabled.Value) {
            return;
        }
        string? sourceID = source?.sourceID;
        try {
            var entry = new LSLogEntry(
                level,
                message,
                sourceID,
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
