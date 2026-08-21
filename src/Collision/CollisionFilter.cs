namespace LSUtils.Collision;

public readonly record struct CollisionFilter(uint Layer, uint Mask) {
    public static CollisionFilter Default => new(1u, uint.MaxValue);

    public bool CanCollideWith(CollisionFilter other)
        => (Mask & other.Layer) != 0u && (other.Mask & Layer) != 0u;
}
