namespace LSUtils;

//using System;

/// <summary>
/// A concrete 2D vector implementation with integer components.
/// This is an engine-agnostic vector type that implements ILSVector2I.
/// </summary>
public struct LSVector2I : ILSVector2I {
    #region Properties

    /// <summary>
    /// Gets the X component of the vector.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y component of the vector.
    /// </summary>
    public int Y { get; }

    #endregion

    #region Static Constants

    private static readonly LSVector2I _zero = new LSVector2I(0, 0);
    private static readonly LSVector2I _one = new LSVector2I(1, 1);
    private static readonly LSVector2I _up = new LSVector2I(0, -1);
    private static readonly LSVector2I _down = new LSVector2I(0, 1);
    private static readonly LSVector2I _right = new LSVector2I(1, 0);
    private static readonly LSVector2I _left = new LSVector2I(-1, 0);

    /// <summary>Gets a vector with both components set to zero.</summary>
    public static LSVector2I Zero => _zero;

    /// <summary>Gets a vector with both components set to one.</summary>
    public static LSVector2I One => _one;

    /// <summary>Gets a unit vector pointing up (0, -1).</summary>
    public static LSVector2I Up => _up;

    /// <summary>Gets a unit vector pointing down (0, 1).</summary>
    public static LSVector2I Down => _down;

    /// <summary>Gets a unit vector pointing right (1, 0).</summary>
    public static LSVector2I Right => _right;

    /// <summary>Gets a unit vector pointing left (-1, 0).</summary>
    public static LSVector2I Left => _left;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LSVector2I"/> struct.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    public LSVector2I(int x, int y) {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSVector2I"/> struct from another vector.
    /// </summary>
    /// <param name="other">The vector to copy from.</param>
    public LSVector2I(ILSVector2I other) {
        X = other.X;
        Y = other.Y;
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Converts this integer vector to a floating-point vector.
    /// </summary>
    /// <returns>A floating-point vector with the same component values.</returns>
    public ILSVector2 ToVector2() => new LSVector2(X, Y);

    #endregion

    #region Basic Math Operations

    /// <summary>Adds another vector to this vector.</summary>
    /// <param name="other">The vector to add.</param>
    /// <returns>The sum of the two vectors.</returns>
    public ILSVector2I Add(ILSVector2I other) => new LSVector2I(X + other.X, Y + other.Y);

    /// <summary>Subtracts another vector from this vector.</summary>
    /// <param name="other">The vector to subtract.</param>
    /// <returns>The difference of the two vectors.</returns>
    public ILSVector2I Subtract(ILSVector2I other) => new LSVector2I(X - other.X, Y - other.Y);

    /// <summary>Multiplies this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The scaled vector.</returns>
    public ILSVector2I Multiply(int scalar) => new LSVector2I(X * scalar, Y * scalar);

    /// <summary>Multiplies this vector component-wise by another vector.</summary>
    /// <param name="other">The vector to multiply by.</param>
    /// <returns>The component-wise product.</returns>
    public ILSVector2I Multiply(ILSVector2I other) => new LSVector2I(X * other.X, Y * other.Y);

    /// <summary>Divides this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The divided vector.</returns>
    public ILSVector2I Divide(int scalar) => new LSVector2I(X / scalar, Y / scalar);

    /// <summary>Divides this vector component-wise by another vector.</summary>
    /// <param name="other">The vector to divide by.</param>
    /// <returns>The component-wise quotient.</returns>
    public ILSVector2I Divide(ILSVector2I other) => new LSVector2I(X / other.X, Y / other.Y);

    /// <summary>Returns the negation of this vector.</summary>
    /// <returns>A vector with negated components.</returns>
    public ILSVector2I Negate() => new LSVector2I(-X, -Y);

    /// <summary>Returns the modulo of this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The modulo result.</returns>
    public ILSVector2I Modulo(int scalar) => new LSVector2I(X % scalar, Y % scalar);

    /// <summary>Returns the component-wise modulo of this vector by another vector.</summary>
    /// <param name="other">The vector to modulo by.</param>
    /// <returns>The component-wise modulo result.</returns>
    public ILSVector2I Modulo(ILSVector2I other) => new LSVector2I(X % other.X, Y % other.Y);

    #endregion

    #region Vector Math

    /// <summary>Returns a vector with absolute values of each component.</summary>
    /// <returns>A vector with absolute components.</returns>
    public ILSVector2I Abs() => new LSVector2I(LSMath.Abs(X), LSMath.Abs(Y));

    /// <summary>Clamps each component of this vector between corresponding min and max vector components.</summary>
    /// <param name="min">The minimum vector.</param>
    /// <param name="max">The maximum vector.</param>
    /// <returns>The clamped vector.</returns>
    public ILSVector2I Clamp(ILSVector2I min, ILSVector2I max) => new LSVector2I(
        LSMath.Clamp(X, min.X, max.X),
        LSMath.Clamp(Y, min.Y, max.Y)
    );

    /// <summary>Clamps each component of this vector between min and max scalar values.</summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The clamped vector.</returns>
    public ILSVector2I Clamp(int min, int max) => new LSVector2I(
        LSMath.Clamp(X, min, max),
        LSMath.Clamp(Y, min, max)
    );

    /// <summary>Returns a normalized (unit length) version of this vector with rounded integer components.</summary>
    /// <returns>The normalized vector, or zero if the length is zero.</returns>
    public ILSVector2I Normalized() {
        float len = Length();
        if (len == 0) return new LSVector2I(0, 0);
        return new LSVector2I((int)LSMath.Round(X / len), (int)LSMath.Round(Y / len));
    }

    /// <summary>Returns a vector with each component set to the maximum of this vector and another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The component-wise maximum vector.</returns>
    public ILSVector2I Max(ILSVector2I other) => new LSVector2I(
        LSMath.Max(X, other.X),
        LSMath.Max(Y, other.Y)
    );

    /// <summary>Returns a vector with each component set to the minimum of this vector and another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The component-wise minimum vector.</returns>
    public ILSVector2I Min(ILSVector2I other) => new LSVector2I(
        LSMath.Min(X, other.X),
        LSMath.Min(Y, other.Y)
    );

    /// <summary>Returns a vector with the sign (-1, 0, or 1) of each component.</summary>
    /// <returns>The sign vector.</returns>
    public ILSVector2I Sign() => new LSVector2I(LSMath.Sign(X), LSMath.Sign(Y));

    #endregion

    #region Scalar Math

    /// <summary>Returns the aspect ratio (X/Y) of this vector.</summary>
    /// <returns>The aspect ratio, or 0 if Y is zero.</returns>
    public float Aspect() => Y == 0 ? 0 : (float)X / Y;

    /// <summary>Returns the squared distance to another vector (faster than DistanceTo()).</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The squared distance.</returns>
    public float DistanceSquaredTo(ILSVector2I other) {
        int dx = X - other.X;
        int dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Returns the distance to another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The distance.</returns>
    public float DistanceTo(ILSVector2I other) => LSMath.Sqrt(DistanceSquaredTo(other));

    /// <summary>Returns the squared length of this vector (faster than Length()).</summary>
    /// <returns>The squared length.</returns>
    public float LengthSquared() => X * X + Y * Y;

    /// <summary>Returns the length (magnitude) of this vector.</summary>
    /// <returns>The length of the vector.</returns>
    public float Length() => LSMath.Sqrt(LengthSquared());

    #endregion

    #region Equality & Hash

    public bool Equals(ILSVector2I? other) {
        if (other is null) return false;
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj) => obj is ILSVector2I v && Equals(v);

    public override int GetHashCode() => System.HashCode.Combine(X, Y);

    #endregion

    #region String Conversion

    public override string ToString() =>
        $"{X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    #endregion

    #region Operators

    public static LSVector2I operator +(LSVector2I a, LSVector2I b) => new LSVector2I(a.X + b.X, a.Y + b.Y);
    public static LSVector2I operator -(LSVector2I a, LSVector2I b) => new LSVector2I(a.X - b.X, a.Y - b.Y);
    public static LSVector2I operator -(LSVector2I a) => new LSVector2I(-a.X, -a.Y);
    public static LSVector2I operator *(LSVector2I a, int scalar) => new LSVector2I(a.X * scalar, a.Y * scalar);
    public static LSVector2I operator *(int scalar, LSVector2I a) => new LSVector2I(a.X * scalar, a.Y * scalar);
    public static LSVector2I operator *(LSVector2I a, LSVector2I b) => new LSVector2I(a.X * b.X, a.Y * b.Y);
    public static LSVector2I operator /(LSVector2I a, int scalar) => new LSVector2I(a.X / scalar, a.Y / scalar);
    public static LSVector2I operator /(LSVector2I a, LSVector2I b) => new LSVector2I(a.X / b.X, a.Y / b.Y);
    public static LSVector2I operator %(LSVector2I a, int scalar) => new LSVector2I(a.X % scalar, a.Y % scalar);
    public static LSVector2I operator %(LSVector2I a, LSVector2I b) => new LSVector2I(a.X % b.X, a.Y % b.Y);

    public static bool operator ==(LSVector2I a, LSVector2I b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(LSVector2I a, LSVector2I b) => !(a == b);

    #endregion
}
