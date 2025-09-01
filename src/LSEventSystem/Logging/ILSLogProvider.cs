namespace LSUtils.EventSystem.Logging;

/// <summary>
/// Interface for log output providers that handle the actual writing of log messages.
/// Implementations can target different output systems like Console, files, Godot, etc.
/// </summary>
public interface ILSLogProvider {
    /// <summary>
    /// Writes a log entry to the target output system.
    /// </summary>
    /// <param name="entry">The complete log entry to write.</param>
    void WriteLog(LSLogEntry entry);

    /// <summary>
    /// Gets the name of this log provider for identification purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates whether this provider is currently available and functional.
    /// </summary>
    bool IsAvailable { get; }
}
