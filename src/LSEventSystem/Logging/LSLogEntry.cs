using System;
using System.Collections.Generic;

namespace LSUtils.EventSystem.Logging;

/// <summary>
/// Defines the severity levels for log messages.
/// Ordered from most verbose (lowest value) to least verbose (highest value).
/// </summary>
public enum LSLogLevel {
    /// <summary>
    /// Detailed information for debugging and development. Most verbose level.
    /// Should be disabled in production for performance.
    /// </summary>
    DEBUG = 0,

    /// <summary>
    /// General information about normal system operation.
    /// Useful for monitoring and understanding system behavior.
    /// </summary>
    INFO = 1,

    /// <summary>
    /// Warnings about potential issues that don't break functionality.
    /// Important information that should be monitored but doesn't require immediate action.
    /// </summary>
    WARNING = 2,

    /// <summary>
    /// Error conditions that break functionality but allow the system to continue.
    /// Requires attention and may need corrective action.
    /// </summary>
    ERROR = 3,

    /// <summary>
    /// Critical errors that could cause system instability or data loss.
    /// Requires immediate attention and intervention.
    /// </summary>
    CRITICAL = 4
}

/// <summary>
/// Represents a complete log entry with all associated metadata.
/// Immutable structure that contains all information needed for logging.
/// </summary>
public readonly struct LSLogEntry {
    /// <summary>
    /// The severity level of this log entry.
    /// </summary>
    public LSLogLevel Level { get; }

    /// <summary>
    /// The main log message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The component or system that generated this log entry.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// UTC timestamp when this log entry was created.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Optional exception associated with this log entry.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Optional structured data associated with this log entry.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Properties { get; }

    /// <summary>
    /// Optional event ID for correlation with specific event instances.
    /// </summary>
    public Guid? EventId { get; }

    /// <summary>
    /// Initializes a new log entry.
    /// </summary>
    public LSLogEntry(
        LSLogLevel level,
        string message,
        string source,
        DateTime? timestamp = null,
        Exception? exception = null,
        IReadOnlyDictionary<string, object>? properties = null,
        Guid? eventId = null) {
        Level = level;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Timestamp = timestamp ?? DateTime.UtcNow;
        Exception = exception;
        Properties = properties;
        EventId = eventId;
    }

    /// <summary>
    /// Creates a formatted string representation of this log entry.
    /// </summary>
    public override string ToString() {
        var levelStr = Level.ToString().PadRight(8);
        var timeStr = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var sourceStr = Source.PadRight(20);
        
        var result = $"{timeStr} [{levelStr}] {sourceStr}: {Message}";
        
        if (EventId.HasValue) {
            result += $" [Event: {EventId.Value:N}]";
        }
        
        if (Exception != null) {
            result += $"\n  Exception: {Exception.GetType().Name}: {Exception.Message}";
            if (!string.IsNullOrEmpty(Exception.StackTrace)) {
                result += $"\n  Stack Trace: {Exception.StackTrace}";
            }
        }
        
        if (Properties != null && Properties.Count > 0) {
            result += "\n  Properties:";
            foreach (var prop in Properties) {
                result += $"\n    {prop.Key}: {prop.Value}";
            }
        }
        
        return result;
    }
}
