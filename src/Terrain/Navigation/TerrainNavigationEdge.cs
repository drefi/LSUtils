namespace LSUtils.Terrain.Navigation;

/// <summary>An immutable edge exposed for navigation diagnostics and visualization.</summary>
public readonly record struct TerrainNavigationEdge(LSVector2 From, LSVector2 To);
