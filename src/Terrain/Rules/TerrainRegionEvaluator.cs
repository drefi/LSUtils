namespace LSUtils.Terrain.Rules;

using System.Collections.Generic;

public static class TerrainRegionEvaluator {
    public static TBiomeType Evaluate<TBiomeType, TTerrainType, TContentType>(
        TerrainRegionEvaluationContext<TTerrainType, TContentType> context,
        IEnumerable<ITerrainRegionRule<TBiomeType, TTerrainType, TContentType>> rules,
        TBiomeType defaultResult) {
        ITerrainRegionRule<TBiomeType, TTerrainType, TContentType>? match = null;

        foreach (var rule in rules) {
            if (!rule.Matches(context)) continue;
            if (match == null || rule.Priority > match.Priority) match = rule;
        }

        return match == null ? defaultResult : match.Result;
    }

    public static TBiomeType Evaluate<TBiomeType, TTerrainType, TContentType>(
        TerrainRegion<TTerrainType, TContentType> region,
        IEnumerable<ITerrainRegionRule<TBiomeType, TTerrainType, TContentType>> rules,
        TBiomeType defaultResult) {
        return Evaluate(new TerrainRegionEvaluationContext<TTerrainType, TContentType>(region), rules, defaultResult);
    }
}
