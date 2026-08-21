namespace LSUtils.Collision;

/// <summary>Contact data returned by a collision query.</summary>
public readonly record struct CollisionContact<T>(T Item, LSVector2 Point, LSVector2 Normal, float Distance);
