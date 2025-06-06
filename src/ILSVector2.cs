namespace LSUtils;

/// <summary>
/// Represents a 2D vector with various mathematical operations.
/// </summary>
public interface ILSVector2 : IEquatable<ILSVector2> {
    // Properties
    float X { get; }
    float Y { get; }

    // Conversion
    ILSVector2I ToVector2I();

    // Basic Math Operations
    ILSVector2 Add(ILSVector2 other);
    ILSVector2 Subtract(ILSVector2 other);
    ILSVector2 Multiply(float scalar);
    ILSVector2 Multiply(ILSVector2 other);
    ILSVector2 Divide(float scalar);
    ILSVector2 Divide(ILSVector2 other);
    ILSVector2 Modulo(float scalar);
    ILSVector2 Modulo(ILSVector2 other);
    ILSVector2 Negate();

    // Vector Math
    ILSVector2 Abs();
    ILSVector2 Ceil();
    ILSVector2 Floor();
    ILSVector2 Round();
    ILSVector2 Inverse();
    ILSVector2 Normalized();
    ILSVector2 Rotated(float angle);
    ILSVector2 Clamp(ILSVector2 min, ILSVector2 max);
    ILSVector2 Clamp(float min, float max);
    ILSVector2 LimitLength(float length);
    ILSVector2 Max(ILSVector2 other);
    ILSVector2 Min(ILSVector2 other);
    ILSVector2 Lerp(ILSVector2 to, float weight);
    ILSVector2 MoveToward(ILSVector2 to, float delta);
    ILSVector2 DirectionTo(ILSVector2 to);

    // Scalar Math
    float Length();
    float LengthSquared();
    float Angle();
    float AngleTo(ILSVector2 to);
    float Cross(ILSVector2 with);
    float Dot(ILSVector2 with);
    float DistanceTo(ILSVector2 other);
    float DistanceSquaredTo(ILSVector2 other);

    // State Checks
    bool IsFinite();
    bool IsNormalized();

    // Operators
    static ILSVector2 operator +(ILSVector2 a, ILSVector2 b) => a.Add(b);
    static ILSVector2 operator -(ILSVector2 a, ILSVector2 b) => a.Subtract(b);
    static ILSVector2 operator -(ILSVector2 a) => a.Negate();
    static ILSVector2 operator *(ILSVector2 a, float scalar) => a.Multiply(scalar);
    static ILSVector2 operator *(float scalar, ILSVector2 a) => a.Multiply(scalar);
    static ILSVector2 operator *(ILSVector2 a, ILSVector2 b) => a.Multiply(b);
    static ILSVector2 operator /(ILSVector2 a, float scalar) => a.Divide(scalar);
    static ILSVector2 operator /(ILSVector2 a, ILSVector2 b) => a.Divide(b);
    static ILSVector2 operator %(ILSVector2 a, float scalar) => a.Modulo(scalar);
    static ILSVector2 operator %(float scalar, ILSVector2 a) => a.Modulo(scalar);
}
