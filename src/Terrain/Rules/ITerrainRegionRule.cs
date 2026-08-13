namespace LSUtils.Terrain.Rules;

public interface ITerrainRegionRule<TBiomeType, TTerrainType, TContentType> {
    int Priority { get; }
    TBiomeType Result { get; }
    bool Matches(TerrainRegionEvaluationContext<TTerrainType, TContentType> context);
}
