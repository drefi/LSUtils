using System.Runtime.CompilerServices;

namespace LSUtils;
/// <summary>
/// Provides mathematical utility functions.
/// </summary>
public static class LSMath {

    /// <summary>
    /// Clamps a value between a minimum and maximum value.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum allowable value.</param>
    /// <param name="max">The maximum allowable value.</param>
    /// <returns>The clamped value.</returns>
    public static int Clamp(int value, int min, int max) {
        return System.Math.Clamp(value, min, max);
    }
    public static float Clamp(float value, float min, float max) {
        //throw new LSException($"Clamp {value} {min} {max}");
        return System.Math.Clamp(value, min, max);
    }
    /// <summary>
    /// Returns the largest integer less than or equal to the specified single-precision floating-point number.
    /// </summary>
    /// <param name="s">The single-precision floating-point number to floor.</param>
    /// <returns>The largest integer less than or equal to the specified number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Floor(float s) {
        return System.MathF.Floor(s);
    }

    /// <summary>
    /// Returns the largest integer less than or equal to the specified double-precision floating-point number.
    /// </summary>
    /// <param name="s">The double-precision floating-point number to floor.</param>
    /// <returns>The largest integer less than or equal to the specified number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Floor(double s) {
        return System.Math.Floor(s);
    }

    /// <summary>
    /// Returns the smallest integer greater than or equal to the specified single-precision floating-point number.
    /// </summary>
    /// <param name="s">The single-precision floating-point number to ceiling.</param>
    /// <returns>The smallest integer greater than or equal to the specified number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Ceil(float s) {
        return System.MathF.Ceiling(s);
    }

    /// <summary>
    /// Returns the smallest integer greater than or equal to the specified double-precision floating-point number.
    /// </summary>
    /// <param name="s">The double-precision floating-point number to ceiling.</param>
    /// <returns>The smallest integer greater than or equal to the specified number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Ceil(double s) {
        return System.Math.Ceiling(s);
    }
}
