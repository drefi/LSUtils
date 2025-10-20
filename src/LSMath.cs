using System.Runtime.CompilerServices;

namespace LSUtils;
/// <summary>
/// Provides mathematical utility functions.
/// </summary>
public static class LSMath {
    public static bool TryParseFloat(string? value, out float result) {
        return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
    }
    public static float ParseFloat(string? value, float defaultValue = 0f) {
        return TryParseFloat(value, out float result) ? result : defaultValue;
    }
    public static bool TryParseInt(string? value, out int result) {
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result);
    }
    /// <summary>
    /// Clamps a value between a minimum and maximum value.
    /// </summary>
    public static int Clamp(int value, int min, int max) {
        return System.Math.Clamp(value, min, max);
    }
    public static float Clamp(float value, float min, float max) {
        return System.Math.Clamp(value, min, max);
    }
    public static T Clamp<T>(T value, T min, T max) where T : System.IComparable<T> {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    /// <summary>
    /// Returns the maximum of two integers.
    /// </summary>
    public static int Max(int a, int b) {
        return System.Math.Max(a, b);
    }

    /// <summary>
    /// Returns the maximum of two floats.
    /// </summary>
    public static float Max(float a, float b) {
        return System.MathF.Max(a, b);
    }

    /// <summary>
    /// Returns the minimum of two integers.
    /// </summary>
    public static int Min(int a, int b) {
        return System.Math.Min(a, b);
    }

    /// <summary>
    /// Returns the minimum of two floats.
    /// </summary>
    public static float Min(float a, float b) {
        return System.MathF.Min(a, b);
    }

    /// <summary>
    /// Returns the largest integer less than or equal to the specified single-precision floating-point number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float s) {
        return System.MathF.Floor(s);
    }

    /// <summary>
    /// Returns the largest integer less than or equal to the specified double-precision floating-point number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Floor(double s) {
        return System.Math.Floor(s);
    }

    /// <summary>
    /// Returns the smallest integer greater than or equal to the specified single-precision floating-point number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceil(float s) {
        return System.MathF.Ceiling(s);
    }

    /// <summary>
    /// Returns the smallest integer greater than or equal to the specified double-precision floating-point number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Ceil(double s) {
        return System.Math.Ceiling(s);
    }
}
