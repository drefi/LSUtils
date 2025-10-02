namespace LSUtils.Logging;
/// <summary>
/// Console-based log provider that outputs to standard console.
/// Default implementation suitable for desktop applications and development.
/// </summary>
public class LSConsoleLogProvider : ILSLogProvider {
    /// <summary>
    /// Gets the name of this log provider.
    /// </summary>
    public string Name => "Console";

    /// <summary>
    /// Indicates whether console output is available.
    /// </summary>
    public bool IsAvailable => true;

    /// <summary>
    /// Writes a log entry to the console with color coding based on log level.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    public void WriteLog(LSLogEntry entry) {
        try {
            // Set console color based on log level
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = entry.Level switch {
                LSLogLevel.DEBUG => System.ConsoleColor.Gray,
                LSLogLevel.INFO => System.ConsoleColor.White,
                LSLogLevel.WARNING => System.ConsoleColor.Yellow,
                LSLogLevel.ERROR => System.ConsoleColor.Red,
                LSLogLevel.CRITICAL => System.ConsoleColor.Magenta,
                _ => System.ConsoleColor.White
            };

            System.Console.WriteLine(entry.ToString());
            System.Console.ForegroundColor = originalColor;
        } catch {
            // Fallback to basic output if color setting fails
            System.Console.WriteLine(entry.ToString());
        }
    }
}

/// <summary>
/// Godot-based log provider that outputs to Godot's debug console.
/// Automatically detects if Godot is available and falls back gracefully.
/// </summary>
public class LSGodotLogProvider : ILSLogProvider {
    private static readonly System.Lazy<bool> _isGodotAvailable = new(CheckGodotAvailability);

    /// <summary>
    /// Gets the name of this log provider.
    /// </summary>
    public string Name => "Godot";

    /// <summary>
    /// Indicates whether Godot output is available.
    /// </summary>
    public bool IsAvailable => _isGodotAvailable.Value;

    /// <summary>
    /// Writes a log entry to Godot's debug console.
    /// </summary>
    /// <param name="entry">The log entry to write.</param>
    public void WriteLog(LSLogEntry entry) {
        if (!IsAvailable) return;

        try {
            // Use reflection to call GD.Print to avoid hard dependency on Godot
            var gdType = System.Type.GetType("Godot.GD, GodotSharp");
            if (gdType != null) {
                var printMethod = entry.Level switch {
                    LSLogLevel.DEBUG => gdType.GetMethod("Print"),
                    LSLogLevel.INFO => gdType.GetMethod("Print"),
                    LSLogLevel.WARNING => gdType.GetMethod("PrintErr"), // Use PrintErr for warnings and above
                    LSLogLevel.ERROR => gdType.GetMethod("PrintErr"),
                    LSLogLevel.CRITICAL => gdType.GetMethod("PrintErr"),
                    _ => gdType.GetMethod("Print")
                };

                printMethod?.Invoke(null, new object[] { entry.ToString() });
            }
        } catch {
            // Silent failure - Godot logging is optional
        }
    }

    private static bool CheckGodotAvailability() {
        try {
            var gdType = System.Type.GetType("Godot.GD, GodotSharp");
            return gdType != null;
        } catch {
            return false;
        }
    }
}

/// <summary>
/// Null log provider that discards all log entries.
/// Useful for disabling logging entirely in production or testing.
/// </summary>
public class LSNullLogProvider : ILSLogProvider {
    /// <summary>
    /// Gets the name of this log provider.
    /// </summary>
    public string Name => "Null";

    /// <summary>
    /// Always returns true as null provider is always "available".
    /// </summary>
    public bool IsAvailable => true;

    /// <summary>
    /// Discards the log entry without any output.
    /// </summary>
    /// <param name="entry">The log entry to discard.</param>
    public void WriteLog(LSLogEntry entry) {
        // Intentionally empty - discard all log entries
    }
}
