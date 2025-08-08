using System;

namespace LSUtils;

public interface ILSVector2I : IEquatable<ILSVector2I>
{
    /// <summary>
    /// Gets the X component of the vector.
    /// </summary>
    int X { get; }

    /// <summary>
    /// Gets the Y component of the vector.
    /// </summary>
    int Y { get; }

    /// <summary>
    /// Converts the vector to a Vector2 (floating-point coordinates).
    /// </summary>
    ILSVector2 ToVector2();

    ILSVector2I Abs();
    ILSVector2I Clamp(ILSVector2I min, ILSVector2I max);
    ILSVector2I Clamp(int min, int max);
    ILSVector2I Normalized();
    float Aspect();
    float DistanceSquaredTo(ILSVector2I other);
    float DistanceTo(ILSVector2I other);
    float LengthSquared();
    float Length();
    ILSVector2I Max(ILSVector2I other);
    ILSVector2I Min(ILSVector2I other);
    ILSVector2I Sign();
    ILSVector2I Add(ILSVector2I other);
    ILSVector2I Subtract(ILSVector2I other);
    ILSVector2I Multiply(int scalar);
    ILSVector2I Multiply(ILSVector2I other);
    ILSVector2I Divide(int scalar);
    ILSVector2I Divide(ILSVector2I other);
    ILSVector2I Negate();
    ILSVector2I Modulo(int scalar);
    ILSVector2I Modulo(ILSVector2I other);

    static ILSVector2I operator +(ILSVector2I a, ILSVector2I b) => a.Add(b);
    static ILSVector2I operator -(ILSVector2I a, ILSVector2I b) => a.Subtract(b);
    static ILSVector2I operator -(ILSVector2I a) => a.Negate();
    static ILSVector2I operator *(ILSVector2I a, int scalar) => a.Multiply(scalar);
    static ILSVector2I operator *(int scalar, ILSVector2I a) => a.Multiply(scalar);
    static ILSVector2I operator *(ILSVector2I a, ILSVector2I b) => a.Multiply(b);
    static ILSVector2I operator /(ILSVector2I a, int scalar) => a.Divide(scalar);
    static ILSVector2I operator /(ILSVector2I a, ILSVector2I b) => a.Divide(b);
    static ILSVector2I operator %(ILSVector2I a, int scalar) => a.Modulo(scalar);
    static ILSVector2I operator %(ILSVector2I a, ILSVector2I b) => a.Modulo(b);
}
