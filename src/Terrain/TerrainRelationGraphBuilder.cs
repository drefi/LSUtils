namespace LSUtils.Terrain;

using System.Collections.Generic;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Graphs;

public static class TerrainRelationGraphBuilder {
    public static RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType> Build<TTerrainType, TContentType>(
        TerrainWorld<TTerrainType, TContentType> world) {
        var graph = new RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType>();

        foreach (var patch in world.Patches) {
            graph.AddNode(patch);

            foreach (var candidate in world.QueryPatches(patch.Bounds)) {
                if (ReferenceEquals(patch, candidate)) continue;
                if (!ShouldEvaluatePair(graph, patch, candidate)) continue;

                AddRelation(graph, patch, candidate);
            }
        }

        return graph;
    }

    public static RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType> Build<TTerrainType>(
        IEnumerable<TerrainPatch<TTerrainType>> patches) {
        var graph = new RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType>();
        var patchList = patches.ToList();

        foreach (var patch in patchList) graph.AddNode(patch);

        for (int i = 0; i < patchList.Count; i++) {
            for (int j = i + 1; j < patchList.Count; j++) {
                AddRelation(graph, patchList[i], patchList[j]);
            }
        }

        return graph;
    }

    private static bool ShouldEvaluatePair<TTerrainType>(
        RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType> graph,
        TerrainPatch<TTerrainType> a,
        TerrainPatch<TTerrainType> b) {
        return !graph.GetRelations(a, b).Any() && !graph.GetRelations(b, a).Any();
    }

    private static void AddRelation<TTerrainType>(
        RelationGraph<TerrainPatch<TTerrainType>, TerrainRelationType> graph,
        TerrainPatch<TTerrainType> a,
        TerrainPatch<TTerrainType> b) {
        var relation = GeometryRelations.Classify(a.Shape, b.Shape);

        switch (relation) {
            case ShapeRelation.Touches:
                graph.AddUndirectedRelation(a, b, TerrainRelationType.Adjacent);
                break;
            case ShapeRelation.Intersects:
                graph.AddUndirectedRelation(a, b, TerrainRelationType.Overlapping);
                break;
            case ShapeRelation.Contains:
                graph.AddRelation(a, b, TerrainRelationType.Contains);
                graph.AddRelation(b, a, TerrainRelationType.ContainedBy);
                break;
            case ShapeRelation.ContainedBy:
                graph.AddRelation(a, b, TerrainRelationType.ContainedBy);
                graph.AddRelation(b, a, TerrainRelationType.Contains);
                break;
        }
    }
}
