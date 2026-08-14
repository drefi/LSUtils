namespace LSUtils.Terrain.Navigation;

/// <summary>An immutable triangle from the baked navigation surface.</summary>
public readonly record struct TerrainNavigationTriangle(LSVector2 A, LSVector2 B, LSVector2 C, float Cost);
