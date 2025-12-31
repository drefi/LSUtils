namespace LSUtils;

using System;

/// <summary>
/// A concrete 2D vector implementation with floating-point components.
/// This is an engine-agnostic vector type that implements ILSVector2.
/// </summary>
public struct LSVector2 : ILSVector2 {
    #region Properties

    /// <summary>
    /// Gets the X component of the vector.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y component of the vector.
    /// </summary>
    public float Y { get; }

    #endregion

    #region Static Constants

    private static readonly LSVector2 _zero = new LSVector2(0f, 0f);
    private static readonly LSVector2 _one = new LSVector2(1f, 1f);
    private static readonly LSVector2 _inf = new LSVector2(float.PositiveInfinity, float.PositiveInfinity);
    private static readonly LSVector2 _up = new LSVector2(0f, -1f);
    private static readonly LSVector2 _down = new LSVector2(0f, 1f);
    private static readonly LSVector2 _right = new LSVector2(1f, 0f);
    private static readonly LSVector2 _left = new LSVector2(-1f, 0f);

    /// <summary>Gets a vector with both components set to zero.</summary>
    public static LSVector2 Zero => _zero;

    /// <summary>Gets a vector with both components set to one.</summary>
    public static LSVector2 One => _one;

    /// <summary>Gets a vector with both components set to positive infinity.</summary>
    public static LSVector2 Inf => _inf;

    /// <summary>Gets a unit vector pointing up (0, -1).</summary>
    public static LSVector2 Up => _up;

    /// <summary>Gets a unit vector pointing down (0, 1).</summary>
    public static LSVector2 Down => _down;

    /// <summary>Gets a unit vector pointing right (1, 0).</summary>
    public static LSVector2 Right => _right;

    /// <summary>Gets a unit vector pointing left (-1, 0).</summary>
    public static LSVector2 Left => _left;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="LSVector2"/> struct.
    /// </summary>
    /// <param name="x">The X component.</param>
    /// <param name="y">The Y component.</param>
    public LSVector2(float x, float y) {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LSVector2"/> struct from another vector.
    /// </summary>
    /// <param name="other">The vector to copy from.</param>
    public LSVector2(ILSVector2 other) {
        X = other.X;
        Y = other.Y;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a unit vector from an angle in radians.
    /// </summary>
    public static LSVector2 FromAngle(float angle) {
        return new LSVector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Converts this vector to an integer vector by rounding each component.
    /// </summary>
    /// <returns>An integer vector with rounded components.</returns>
    public ILSVector2I ToVector2I() => new LSVector2I((int)MathF.Round(X), (int)MathF.Round(Y));

    #endregion

    #region Basic Math Operations

    /// <summary>Adds another vector to this vector.</summary>
    /// <param name="other">The vector to add.</param>
    /// <returns>The sum of the two vectors.</returns>
    public ILSVector2 Add(ILSVector2 other) => new LSVector2(X + other.X, Y + other.Y);

    /// <summary>Subtracts another vector from this vector.</summary>
    /// <param name="other">The vector to subtract.</param>
    /// <returns>The difference of the two vectors.</returns>
    public ILSVector2 Subtract(ILSVector2 other) => new LSVector2(X - other.X, Y - other.Y);

    /// <summary>Multiplies this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The scaled vector.</returns>
    public ILSVector2 Multiply(float scalar) => new LSVector2(X * scalar, Y * scalar);

    /// <summary>Multiplies this vector component-wise by another vector.</summary>
    /// <param name="other">The vector to multiply by.</param>
    /// <returns>The component-wise product.</returns>
    public ILSVector2 Multiply(ILSVector2 other) => new LSVector2(X * other.X, Y * other.Y);

    /// <summary>Divides this vector by a scalar value.</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The divided vector.</returns>
    public ILSVector2 Divide(float scalar) => new LSVector2(X / scalar, Y / scalar);

    /// <summary>Divides this vector component-wise by another vector.</summary>
    /// <param name="other">The vector to divide by.</param>
    /// <returns>The component-wise quotient.</returns>
    public ILSVector2 Divide(ILSVector2 other) => new LSVector2(X / other.X, Y / other.Y);

    /// <summary>Returns the modulo of this vector by a scalar value (always positive).</summary>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>The modulo result.</returns>
    public ILSVector2 Modulo(float scalar) {
        float modX = X % scalar;
        float modY = Y % scalar;
        return new LSVector2(
            modX < 0 ? modX + scalar : modX,
            modY < 0 ? modY + scalar : modY
        );
    }

    /// <summary>Returns the component-wise modulo of this vector by another vector (always positive).</summary>
    /// <param name="other">The vector to modulo by.</param>
    /// <returns>The component-wise modulo result.</returns>
    public ILSVector2 Modulo(ILSVector2 other) {
        float modX = X % other.X;
        float modY = Y % other.Y;
        return new LSVector2(
            modX < 0 ? modX + other.X : modX,
            modY < 0 ? modY + other.Y : modY
        );
    }

    /// <summary>Returns the negation of this vector.</summary>
    /// <returns>A vector with negated components.</returns>
    public ILSVector2 Negate() => new LSVector2(-X, -Y);

    #endregion

    #region Vector Math

    /// <summary>Returns a vector with absolute values of each component.</summary>
    /// <returns>A vector with absolute components.</returns>
    public ILSVector2 Abs() => new LSVector2(MathF.Abs(X), MathF.Abs(Y));

    /// <summary>Returns a vector with each component rounded up to the nearest integer.</summary>
    /// <returns>A vector with ceiling values.</returns>
    public ILSVector2 Ceil() => new LSVector2(MathF.Ceiling(X), MathF.Ceiling(Y));

    /// <summary>Returns a vector with each component rounded down to the nearest integer.</summary>
    /// <returns>A vector with floor values.</returns>
    public ILSVector2 Floor() => new LSVector2(MathF.Floor(X), MathF.Floor(Y));

    /// <summary>Returns a vector with each component rounded to the nearest integer.</summary>
    /// <returns>A vector with rounded values.</returns>
    public ILSVector2 Round() => new LSVector2(MathF.Round(X), MathF.Round(Y));

    /// <summary>Returns the component-wise inverse (1/x, 1/y) of this vector. Zero components remain zero.</summary>
    /// <returns>The inverse vector.</returns>
    public ILSVector2 Inverse() => new LSVector2(
        X != 0 ? 1f / X : 0f,
        Y != 0 ? 1f / Y : 0f
    );

    /// <summary>Returns a normalized (unit length) version of this vector.</summary>
    /// <returns>The normalized vector, or zero if the length is zero.</returns>
    public ILSVector2 Normalized() {
        float len = Length();
        return len == 0 ? new LSVector2(0, 0) : new LSVector2(X / len, Y / len);
    }

    /// <summary>Returns this vector rotated by the specified angle in radians.</summary>
    /// <param name="angle">The rotation angle in radians.</param>
    /// <returns>The rotated vector.</returns>
    public ILSVector2 Rotated(float angle) {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new LSVector2(
            X * cos - Y * sin,
            X * sin + Y * cos
        );
    }

    /// <summary>Clamps each component of this vector between corresponding min and max vector components.</summary>
    /// <param name="min">The minimum vector.</param>
    /// <param name="max">The maximum vector.</param>
    /// <returns>The clamped vector.</returns>
    public ILSVector2 Clamp(ILSVector2 min, ILSVector2 max) => new LSVector2(
        Math.Clamp(X, min.X, max.X),
        Math.Clamp(Y, min.Y, max.Y)
    );

    /// <summary>Clamps each component of this vector between min and max scalar values.</summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>The clamped vector.</returns>
    public ILSVector2 Clamp(float min, float max) => new LSVector2(
        Math.Clamp(X, min, max),
        Math.Clamp(Y, min, max)
    );

    /// <summary>Returns this vector with its length limited to the specified value.</summary>
    /// <param name="length">The maximum length.</param>
    /// <returns>The length-limited vector.</returns>
    public ILSVector2 LimitLength(float length) {
        float len = Length();
        if (len > length && len > 0) {
            float scale = length / len;
            return new LSVector2(X * scale, Y * scale);
        }
        return this;
    }

    /// <summary>Returns a vector with each component set to the maximum of this vector and another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The component-wise maximum vector.</returns>
    public ILSVector2 Max(ILSVector2 other) => new LSVector2(
        MathF.Max(X, other.X),
        MathF.Max(Y, other.Y)
    );

    /// <summary>Returns a vector with each component set to the minimum of this vector and another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The component-wise minimum vector.</returns>
    public ILSVector2 Min(ILSVector2 other) => new LSVector2(
        MathF.Min(X, other.X),
        MathF.Min(Y, other.Y)
    );

    /// <summary>Linearly interpolates between this vector and another by the given weight.</summary>
    /// <param name="to">The target vector.</param>
    /// <param name="weight">The interpolation weight (0-1).</param>
    /// <returns>The interpolated vector.</returns>
    public ILSVector2 Lerp(ILSVector2 to, float weight) => new LSVector2(
        X + (to.X - X) * weight,
        Y + (to.Y - Y) * weight
    );

    /// <summary>Moves this vector toward a target vector by the specified delta distance.</summary>
    /// <param name="to">The target vector.</param>
    /// <param name="delta">The maximum distance to move.</param>
    /// <returns>The moved vector.</returns>
    public ILSVector2 MoveToward(ILSVector2 to, float delta) {
        float dx = to.X - X;
        float dy = to.Y - Y;
        float distSq = dx * dx + dy * dy;

        if (distSq == 0 || distSq <= delta * delta) {
            return new LSVector2(to.X, to.Y);
        }

        float dist = MathF.Sqrt(distSq);
        return new LSVector2(
            X + dx / dist * delta,
            Y + dy / dist * delta
        );
    }

    /// <summary>Returns the normalized direction vector from this vector to another vector.</summary>
    /// <param name="to">The target vector.</param>
    /// <returns>The direction vector, or zero if the vectors are the same.</returns>
    public ILSVector2 DirectionTo(ILSVector2 to) {
        float dx = to.X - X;
        float dy = to.Y - Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        return length == 0 ? new LSVector2(0, 0) : new LSVector2(dx / length, dy / length);
    }

    #endregion

    #region Scalar Math

    /// <summary>Returns the length (magnitude) of this vector.</summary>
    /// <returns>The length of the vector.</returns>
    public float Length() => MathF.Sqrt(X * X + Y * Y);

    /// <summary>Returns the squared length of this vector (faster than Length()).</summary>
    /// <returns>The squared length.</returns>
    public float LengthSquared() => X * X + Y * Y;

    /// <summary>Returns the angle of this vector in radians.</summary>
    /// <returns>The angle in radians.</returns>
    public float Angle() => MathF.Atan2(Y, X);

    /// <summary>Returns the angle in radians to another vector.</summary>
    /// <param name="to">The target vector.</param>
    /// <returns>The angle in radians.</returns>
    public float AngleTo(ILSVector2 to) {
        float dot = X * to.X + Y * to.Y;
        float det = X * to.Y - Y * to.X;
        return MathF.Atan2(det, dot);
    }

    /// <summary>Returns the 2D cross product (perpendicular dot product) with another vector.</summary>
    /// <param name="with">The other vector.</param>
    /// <returns>The cross product scalar value.</returns>
    public float Cross(ILSVector2 with) => X * with.Y - Y * with.X;

    /// <summary>Returns the dot product with another vector.</summary>
    /// <param name="with">The other vector.</param>
    /// <returns>The dot product.</returns>
    public float Dot(ILSVector2 with) => X * with.X + Y * with.Y;

    /// <summary>Returns the distance to another vector.</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The distance.</returns>
    public float DistanceTo(ILSVector2 other) {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Returns the squared distance to another vector (faster than DistanceTo()).</summary>
    /// <param name="other">The other vector.</param>
    /// <returns>The squared distance.</returns>
    public float DistanceSquaredTo(ILSVector2 other) {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    #endregion

    #region State Checks

    /// <summary>Returns whether both components are finite (not infinity or NaN).</summary>
    /// <returns>True if both components are finite.</returns>
    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y);

    /// <summary>Returns whether this vector is normalized (has length approximately 1).</summary>
    /// <returns>True if the vector is normalized.</returns>
    public bool IsNormalized() => MathF.Abs(LengthSquared() - 1f) < 1e-6f;

    #endregion

    #region Equality & Hash

    public bool Equals(ILSVector2? other) {
        if (other is null) return false;
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj) => obj is ILSVector2 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    #endregion

    #region String Conversion

    public override string ToString() =>
        $"{X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    #endregion

    #region Operators

    public static LSVector2 operator +(LSVector2 a, LSVector2 b) => new LSVector2(a.X + b.X, a.Y + b.Y);
    public static LSVector2 operator -(LSVector2 a, LSVector2 b) => new LSVector2(a.X - b.X, a.Y - b.Y);
    public static LSVector2 operator -(LSVector2 a) => new LSVector2(-a.X, -a.Y);
    public static LSVector2 operator *(LSVector2 a, float scalar) => new LSVector2(a.X * scalar, a.Y * scalar);
    public static LSVector2 operator *(float scalar, LSVector2 a) => new LSVector2(a.X * scalar, a.Y * scalar);
    public static LSVector2 operator *(LSVector2 a, LSVector2 b) => new LSVector2(a.X * b.X, a.Y * b.Y);
    public static LSVector2 operator /(LSVector2 a, float scalar) => new LSVector2(a.X / scalar, a.Y / scalar);
    public static LSVector2 operator /(LSVector2 a, LSVector2 b) => new LSVector2(a.X / b.X, a.Y / b.Y);
    public static LSVector2 operator %(LSVector2 a, float scalar) => (LSVector2)a.Modulo(scalar);
    public static LSVector2 operator %(float scalar, LSVector2 a) => (LSVector2)a.Modulo(scalar);

    public static bool operator ==(LSVector2 a, LSVector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(LSVector2 a, LSVector2 b) => !(a == b);

    #endregion
}
