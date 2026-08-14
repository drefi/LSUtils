namespace LSUtils.Terrain.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Graphs;
using LSUtils.Graphs.Algorithms;
using LSUtils.Spatial;

/// <summary>
/// Continuous-space pathfinding for polygonal terrain worlds.
/// It builds a visibility graph from the start, goal and obstacle vertices;
/// it never creates a navigation grid.
/// </summary>
public static class TerrainPathfinder {
    public static List<LSVector2> FindPath<TTerrainType, TContentType>(
        TerrainWorld<TTerrainType, TContentType> world,
        LSVector2 start,
        LSVector2 goal,
        TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        if (world == null) throw new LSArgumentNullException(nameof(world));
        if (settings == null) throw new LSArgumentNullException(nameof(settings));

        var graph = new VisibilityGraph<TTerrainType, TContentType>(world, start, goal, settings);
        if (!graph.HasNode(graph.Start) || !graph.HasNode(graph.Goal)) return new List<LSVector2>();

        var path = GraphAlgorithms.AStar(
            graph,
            graph.Start,
            graph.Goal,
            (from, to) => from.DistanceTo(to) * settings.MinimumCost,
            graph.GetTravelCost);
        return graph.Simplify(path);
    }

    private sealed class VisibilityGraph<TTerrainType, TContentType> : IGraph<LSVector2> {
        private const float Epsilon = 0.001f;
        private readonly TerrainWorld<TTerrainType, TContentType> _world;
        private readonly TerrainNavigationSettings<TTerrainType, TContentType> _settings;
        private readonly List<Polygon2D> _obstacles;
        private readonly List<Polygon2D> _costAreas;
        private readonly Dictionary<LSVector2, List<LSVector2>> _neighbors = new();

        public LSVector2 Start { get; }
        public LSVector2 Goal { get; }
        public IEnumerable<LSVector2> Nodes => _neighbors.Keys;

        public VisibilityGraph(
            TerrainWorld<TTerrainType, TContentType> world,
            LSVector2 start,
            LSVector2 goal,
            TerrainNavigationSettings<TTerrainType, TContentType> settings) {
            _world = world;
            _settings = settings;
            _obstacles = GetObstacles().ToList();
            _costAreas = GetCostAreas().ToList();
            Start = FindNearestWalkable(start);
            Goal = FindNearestWalkable(goal);

            AddNode(Start);
            AddNode(Goal);
            foreach (var obstacle in _obstacles) {
                foreach (var vertex in GetClearanceVertices(obstacle)) AddNode(vertex);
            }
            foreach (var area in _costAreas) {
                foreach (var vertex in GetClearanceVertices(area)) AddNode(vertex);
            }

            var nodes = _neighbors.Keys.ToArray();
            for (int i = 0; i < nodes.Length; i++) {
                for (int j = i + 1; j < nodes.Length; j++) {
                    if (!CanTraverse(nodes[i], nodes[j])) continue;
                    _neighbors[nodes[i]].Add(nodes[j]);
                    _neighbors[nodes[j]].Add(nodes[i]);
                }
            }
        }

        public bool HasNode(LSVector2 node) => _neighbors.ContainsKey(node);
        public IEnumerable<LSVector2> GetNeighbors(LSVector2 node) => _neighbors.TryGetValue(node, out var neighbors) ? neighbors : Enumerable.Empty<LSVector2>();

        public float GetTravelCost(LSVector2 from, LSVector2 to) {
            float distance = from.DistanceTo(to);
            int samples = Math.Max(1, (int)MathF.Ceiling(distance / 16f));
            float cost = 0f;
            var delta = to - from;

            for (int sample = 0; sample < samples; sample++) {
                float t = (sample + 0.5f) / samples;
                var point = from + delta * t;
                float terrainCost = _settings.GetTerrainCost(_world.ResolvePatchAt(point.X, point.Y));
                if (terrainCost <= 0f) return float.PositiveInfinity;
                cost += distance / samples * terrainCost;
            }

            return cost;
        }

        public List<LSVector2> Simplify(IReadOnlyList<LSVector2> path) {
            if (path.Count < 3) return path.ToList();

            var simplified = new List<LSVector2> { path[0] };
            int current = 0;
            while (current < path.Count - 1) {
                int next = current + 1;
                float accumulatedCost = 0f;

                for (int candidate = current + 1; candidate < path.Count; candidate++) {
                    accumulatedCost += GetTravelCost(path[candidate - 1], path[candidate]);
                    float directCost = GetTravelCost(path[current], path[candidate]);
                    if (!CanTraverse(path[current], path[candidate])
                        || float.IsPositiveInfinity(directCost)
                        || directCost > accumulatedCost + Epsilon) continue;
                    next = candidate;
                }

                simplified.Add(path[next]);
                current = next;
            }

            return simplified;
        }

        private void AddNode(LSVector2 node) {
            if (!IsWalkable(node) || _neighbors.ContainsKey(node)) return;
            _neighbors.Add(node, new List<LSVector2>());
        }

        private IEnumerable<Polygon2D> GetObstacles() {
            foreach (var patch in _world.Patches) {
                if (_settings.GetTerrainCost(patch) <= 0f && patch.Shape is Polygon2D polygon) yield return polygon;
            }

            foreach (var content in _world.Contents) {
                if (_settings.BlocksContent(content) && content.Shape is Polygon2D polygon) yield return polygon;
            }
        }

        private IEnumerable<Polygon2D> GetCostAreas() {
            foreach (var patch in _world.Patches) {
                float cost = _settings.GetTerrainCost(patch);
                if (cost > _settings.MinimumCost && patch.Shape is Polygon2D polygon) yield return polygon;
            }
        }

        private IEnumerable<LSVector2> GetClearanceVertices(Polygon2D polygon) {
            var center = new LSVector2(
                polygon.Vertices.Average(vertex => vertex.X),
                polygon.Vertices.Average(vertex => vertex.Y));
            float offset = _settings.AgentRadius <= 0f ? Epsilon : _settings.AgentRadius * 1.5f + Epsilon;

            foreach (var vertex in polygon.Vertices) {
                var direction = new LSVector2((vertex - center).Normalized());
                yield return vertex + direction * offset;
            }
        }

        private LSVector2 FindNearestWalkable(LSVector2 position) {
            if (IsWalkable(position)) return position;

            float step = MathF.Max(8f, _settings.AgentRadius * 2f + 1f);
            for (int radius = 1; radius <= 24; radius++) {
                for (int sample = 0; sample < 32; sample++) {
                    float angle = MathF.Tau * sample / 32f;
                    var candidate = position + LSVector2.FromAngle(angle) * step * radius;
                    if (IsWalkable(candidate)) return candidate;
                }
            }

            return position;
        }

        private bool IsWalkable(LSVector2 point) {
            if (!_world.Bounds.Contains(point.X, point.Y)) return false;
            if (_settings.GetTerrainCost(_world.ResolvePatchAt(point.X, point.Y)) <= 0f) return false;
            if (_world.QueryContents(new Bounds(point.X, point.Y, _settings.AgentRadius * 2f, _settings.AgentRadius * 2f))
                .Any(content => _settings.BlocksContent(content) && PointToShapeDistance(point, content.Shape) <= _settings.AgentRadius + Epsilon)) return false;

            return _obstacles.All(obstacle => PointToPolygonDistance(point, obstacle) > _settings.AgentRadius + Epsilon);
        }

        private bool CanTraverse(LSVector2 from, LSVector2 to) {
            if (!IsWalkable(from) || !IsWalkable(to)) return false;
            foreach (var obstacle in _obstacles) {
                if (SegmentToPolygonDistance(from, to, obstacle) <= _settings.AgentRadius + Epsilon) return false;
            }
            return !float.IsPositiveInfinity(GetTravelCost(from, to));
        }

        private static float PointToShapeDistance(LSVector2 point, IShape2D shape) {
            return shape is Polygon2D polygon ? PointToPolygonDistance(point, polygon) : shape.Contains(point.X, point.Y) ? 0f : float.PositiveInfinity;
        }

        private static float PointToPolygonDistance(LSVector2 point, Polygon2D polygon) {
            if (polygon.Contains(point.X, point.Y)) return 0f;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < polygon.Vertices.Count; index++) {
                var a = polygon.Vertices[index];
                var b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
                distance = MathF.Min(distance, PointToSegmentDistance(point, a, b));
            }
            return distance;
        }

        private static float SegmentToPolygonDistance(LSVector2 from, LSVector2 to, Polygon2D polygon) {
            if (polygon.Contains(from.X, from.Y) || polygon.Contains(to.X, to.Y)) return 0f;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < polygon.Vertices.Count; index++) {
                var a = polygon.Vertices[index];
                var b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
                if (SegmentsIntersect(from, to, a, b)) return 0f;
                distance = MathF.Min(distance, SegmentToSegmentDistance(from, to, a, b));
            }
            return distance;
        }

        private static float PointToSegmentDistance(LSVector2 point, LSVector2 a, LSVector2 b) {
            var segment = b - a;
            float lengthSquared = segment.LengthSquared();
            if (lengthSquared <= Epsilon) return point.DistanceTo(a);
            float t = Math.Clamp((point - a).Dot(segment) / lengthSquared, 0f, 1f);
            return point.DistanceTo(a + segment * t);
        }

        private static float SegmentToSegmentDistance(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
            return MathF.Min(MathF.Min(PointToSegmentDistance(a, c, d), PointToSegmentDistance(b, c, d)),
                MathF.Min(PointToSegmentDistance(c, a, b), PointToSegmentDistance(d, a, b)));
        }

        private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon))
                && ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;

            return MathF.Abs(abC) <= Epsilon && PointToSegmentDistance(c, a, b) <= Epsilon
                || MathF.Abs(abD) <= Epsilon && PointToSegmentDistance(d, a, b) <= Epsilon
                || MathF.Abs(cdA) <= Epsilon && PointToSegmentDistance(a, c, d) <= Epsilon
                || MathF.Abs(cdB) <= Epsilon && PointToSegmentDistance(b, c, d) <= Epsilon;
        }

        private static float Cross(LSVector2 a, LSVector2 b) => a.Cross(b);
    }
}
