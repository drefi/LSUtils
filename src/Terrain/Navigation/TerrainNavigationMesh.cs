namespace LSUtils.Terrain.Navigation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LSUtils.Geometry;
using LSUtils.Geometry.Triangulation;
using LSUtils.Graphs;
using LSUtils.Graphs.Algorithms;
using LSUtils.Spatial;

/// <summary>
/// Cached visibility topology for one navigation profile in a terrain world.
/// Rebuild it after changing patches or contents; individual route queries only
/// connect their start and goal to the cached topology.
/// </summary>
public sealed class TerrainNavigationMesh<TTerrainType, TContentType> {
    private const float Epsilon = 0.01f;
    private const int CornerArcSegments = 6;
    private const int ComponentBridgeCandidateCount = 8;
    private const int LocalVisibilityNeighborCount = 16;
    private readonly TerrainWorld<TTerrainType, TContentType> _world;
    private readonly TerrainNavigationSettings<TTerrainType, TContentType> _settings;
    private readonly List<Polygon2D> _obstacles;
    private readonly Dictionary<LSVector2, List<LSVector2>> _topology = new();
    private readonly List<NavigationTriangle> _navigationTriangles = new();
    private readonly List<float> _triangleCosts = new();
    private readonly List<TerrainPatch<TTerrainType>?> _trianglePatches = new();
    private readonly Dictionary<int, List<int>> _triangleNeighbors = new();
    private readonly Dictionary<(int First, int Second), TerrainNavigationEdge> _trianglePortals = new();
    private bool _buildingStaticBake = true;
    private long _visibilityTests;
    private long _visibleConnections;
    private long _obstacleCandidateChecks;
    private long _terrainCostSamples;

    internal TerrainNavigationMesh(TerrainWorld<TTerrainType, TContentType> world, TerrainNavigationSettings<TTerrainType, TContentType> settings) {
        var stopwatch = Stopwatch.StartNew();
        _world = world;
        _settings = settings;
        _obstacles = GetObstacles().ToList();
        BuiltVersion = world.StaticNavigationVersion;

        BuildConstrainedTopology();
        var nodes = _topology.Keys.ToList();
        BuildTriangleAdjacency(nodes);
        AddLocalVisibilityConnections(nodes);
        RepairDisconnectedComponents(nodes);
        Nodes = nodes.AsReadOnly();
        Edges = BuildEdgeSnapshot(nodes).AsReadOnly();
        Triangles = _navigationTriangles
            .Select((triangle, index) => new TerrainNavigationTriangle(nodes[triangle.A], nodes[triangle.B], nodes[triangle.C], _triangleCosts[index]))
            .ToList()
            .AsReadOnly();
        _buildingStaticBake = false;
        stopwatch.Stop();
        BuildStatistics = new TerrainNavigationBuildStatistics(_obstacles.Count, NodeCount, EdgeCount, _visibilityTests, _visibleConnections, _obstacleCandidateChecks, _terrainCostSamples, stopwatch.Elapsed);
    }

    public long BuiltVersion { get; }
    public bool IsCurrent => _world.StaticNavigationVersion == BuiltVersion;
    public int NodeCount => _topology.Count;
    public int EdgeCount => _topology.Sum(pair => pair.Value.Count) / 2;
    public TerrainNavigationBuildStatistics BuildStatistics { get; }
    public IReadOnlyList<LSVector2> Nodes { get; }
    public IReadOnlyList<TerrainNavigationEdge> Edges { get; }
    public IReadOnlyList<TerrainNavigationTriangle> Triangles { get; }

    public TerrainPatch<TTerrainType>? GetTrianglePatch(int triangleIndex) {
        if (triangleIndex < 0 || triangleIndex >= _trianglePatches.Count) throw new ArgumentOutOfRangeException(nameof(triangleIndex));
        return _trianglePatches[triangleIndex];
    }

    private List<TerrainNavigationEdge> BuildEdgeSnapshot(IReadOnlyList<LSVector2> nodes) {
        var indices = nodes.Select((node, index) => (node, index)).ToDictionary(pair => pair.node, pair => pair.index);
        var edges = new List<TerrainNavigationEdge>(EdgeCount);
        foreach (var node in nodes) {
            foreach (var neighbor in _topology[node]) {
                if (indices[node] < indices[neighbor]) edges.Add(new TerrainNavigationEdge(node, neighbor));
            }
        }
        return edges;
    }

    private void BuildConstrainedTopology() {
        if (!TryGetNavigationBounds(out var navigationBounds)) return;
        var constraints = new List<TriangulationConstraint>();
        AddConstraintLoop(constraints, new[] {
            new LSVector2(navigationBounds.MinX, navigationBounds.MinY),
            new LSVector2(navigationBounds.MaxX, navigationBounds.MinY),
            new LSVector2(navigationBounds.MaxX, navigationBounds.MaxY),
            new LSVector2(navigationBounds.MinX, navigationBounds.MaxY)
        }, navigationBounds);
        foreach (var patch in _world.Patches) {
            if (_settings.GetTerrainCost(patch) <= 0f) continue;
            AddConstraintLoop(constraints, RequirePolygon(patch.Shape, "terrain cost boundary").Vertices, navigationBounds);
        }
        foreach (var obstacle in _obstacles) {
            AddConstraintLoop(constraints, GetClearanceArcVertices(obstacle).ToList(), navigationBounds);
        }

        var triangulation = ConstrainedTriangulation2D.Triangulate(constraints);
        var nodeIndices = new Dictionary<QuantizedPoint, int>();
        var topologyNodes = new List<LSVector2>();
        var acceptedEdges = new HashSet<(int A, int B)>();
        foreach (var sourceTriangle in triangulation.Triangles) {
            var first = triangulation.Vertices[sourceTriangle.A];
            var second = triangulation.Vertices[sourceTriangle.B];
            var third = triangulation.Vertices[sourceTriangle.C];
            float area2 = MathF.Abs((second - first).Cross(third - first));
            if (!first.IsFinite() || !second.IsFinite() || !third.IsFinite() || area2 <= Epsilon) continue;
            var sample = (first + second + third) / 3f;
            if (!IsWalkable(sample)) continue;

            var patch = _world.ResolvePatchAt(sample.X, sample.Y);
            float cost = _settings.GetTerrainCost(patch);
            if (cost <= 0f) continue;
            int a = GetOrAddTopologyNode(first, nodeIndices, topologyNodes);
            int b = GetOrAddTopologyNode(second, nodeIndices, topologyNodes);
            int c = GetOrAddTopologyNode(third, nodeIndices, topologyNodes);
            if (a == b || b == c || c == a) continue;

            _navigationTriangles.Add(new NavigationTriangle(a, b, c));
            _triangleCosts.Add(cost);
            _trianglePatches.Add(patch);
            AddTriangleEdge(acceptedEdges, topologyNodes, a, b);
            AddTriangleEdge(acceptedEdges, topologyNodes, b, c);
            AddTriangleEdge(acceptedEdges, topologyNodes, c, a);
        }
    }

    private bool TryGetNavigationBounds(out Bounds navigationBounds) {
        float inset = _settings.AgentRadius + Epsilon;
        var bounds = _world.Bounds;
        navigationBounds = new Bounds(bounds.X, bounds.Y, bounds.Width - inset * 2f, bounds.Height - inset * 2f);
        return navigationBounds.Width > 0f && navigationBounds.Height > 0f;
    }

    private static void AddConstraintLoop(List<TriangulationConstraint> constraints, IReadOnlyList<LSVector2> vertices, Bounds clipBounds) {
        for (int index = 0; index < vertices.Count; index++) {
            if (TryClipSegment(vertices[index], vertices[(index + 1) % vertices.Count], clipBounds, out var from, out var to)) {
                constraints.Add(new TriangulationConstraint(from, to));
            }
        }
    }

    private static bool TryClipSegment(LSVector2 from, LSVector2 to, Bounds bounds, out LSVector2 clippedFrom, out LSVector2 clippedTo) {
        double minimum = 0d, maximum = 1d;
        double deltaX = to.X - from.X, deltaY = to.Y - from.Y;
        if (!Clip(-deltaX, from.X - bounds.MinX, ref minimum, ref maximum)
            || !Clip(deltaX, bounds.MaxX - from.X, ref minimum, ref maximum)
            || !Clip(-deltaY, from.Y - bounds.MinY, ref minimum, ref maximum)
            || !Clip(deltaY, bounds.MaxY - from.Y, ref minimum, ref maximum)) {
            clippedFrom = clippedTo = default;
            return false;
        }
        clippedFrom = minimum <= 0d ? from : new LSVector2((float)(from.X + deltaX * minimum), (float)(from.Y + deltaY * minimum));
        clippedTo = maximum >= 1d ? to : new LSVector2((float)(from.X + deltaX * maximum), (float)(from.Y + deltaY * maximum));
        return clippedFrom != clippedTo;
    }

    private static bool Clip(double direction, double distance, ref double minimum, ref double maximum) {
        if (Math.Abs(direction) <= double.Epsilon) return distance >= 0d;
        double ratio = distance / direction;
        if (direction < 0d) {
            if (ratio > maximum) return false;
            if (ratio > minimum) minimum = ratio;
        } else {
            if (ratio < minimum) return false;
            if (ratio < maximum) maximum = ratio;
        }
        return true;
    }

    private int GetOrAddTopologyNode(LSVector2 point, Dictionary<QuantizedPoint, int> nodeIndices, List<LSVector2> topologyNodes) {
        var key = QuantizedPoint.From(point);
        if (nodeIndices.TryGetValue(key, out int index)) return index;
        index = _topology.Count;
        _topology.Add(point, new List<LSVector2>());
        topologyNodes.Add(point);
        nodeIndices.Add(key, index);
        return index;
    }

    private void AddTriangleEdge(HashSet<(int A, int B)> edges, IReadOnlyList<LSVector2> nodes, int first, int second) {
        var edge = first < second ? (first, second) : (second, first);
        if (!edges.Add(edge)) return;
        _topology[nodes[first]].Add(nodes[second]);
        _topology[nodes[second]].Add(nodes[first]);
    }

    private void BuildTriangleAdjacency(IReadOnlyList<LSVector2> nodes) {
        var edgeOwners = new Dictionary<(int A, int B), int>();
        for (int triangleIndex = 0; triangleIndex < _navigationTriangles.Count; triangleIndex++) {
            _triangleNeighbors.Add(triangleIndex, new List<int>());
            var triangle = _navigationTriangles[triangleIndex];
            RegisterTriangleEdge(edgeOwners, nodes, triangleIndex, triangle.A, triangle.B);
            RegisterTriangleEdge(edgeOwners, nodes, triangleIndex, triangle.B, triangle.C);
            RegisterTriangleEdge(edgeOwners, nodes, triangleIndex, triangle.C, triangle.A);
        }
    }

    private void RegisterTriangleEdge(Dictionary<(int A, int B), int> owners, IReadOnlyList<LSVector2> nodes, int triangleIndex, int first, int second) {
        var edge = first < second ? (first, second) : (second, first);
        if (!owners.TryGetValue(edge, out int otherTriangle)) {
            owners.Add(edge, triangleIndex);
            return;
        }

        _triangleNeighbors[triangleIndex].Add(otherTriangle);
        _triangleNeighbors[otherTriangle].Add(triangleIndex);
        var trianglePair = triangleIndex < otherTriangle ? (triangleIndex, otherTriangle) : (otherTriangle, triangleIndex);
        _trianglePortals[trianglePair] = new TerrainNavigationEdge(nodes[first], nodes[second]);
    }

    private void RepairDisconnectedComponents(IReadOnlyList<LSVector2> nodes) {
        var components = new DisjointSet(nodes.Count);
        var indices = nodes.Select((node, index) => (node, index)).ToDictionary(pair => pair.node, pair => pair.index);
        foreach (var (node, index) in indices) {
            foreach (var neighbor in _topology[node]) components.Union(index, indices[neighbor]);
        }
        if (nodes.Count == 0 || components.ComponentCount == 1) return;

        var candidates = new List<(float DistanceSquared, int First, int Second)>();
        var seen = new HashSet<(int First, int Second)>();
        for (int first = 0; first < nodes.Count; first++) {
            var nearest = Enumerable.Range(0, nodes.Count)
                .Where(second => second != first && components.Find(first) != components.Find(second))
                .OrderBy(second => (nodes[first] - nodes[second]).LengthSquared())
                .Take(ComponentBridgeCandidateCount);
            foreach (int second in nearest) {
                var edge = first < second ? (first, second) : (second, first);
                if (seen.Add(edge)) candidates.Add(((nodes[first] - nodes[second]).LengthSquared(), edge.Item1, edge.Item2));
            }
        }

        foreach (var candidate in candidates.OrderBy(candidate => candidate.DistanceSquared)) {
            if (components.Find(candidate.First) == components.Find(candidate.Second)) continue;
            _visibilityTests++;
            if (!CanTraverse(nodes[candidate.First], nodes[candidate.Second])) continue;
            _visibleConnections++;
            _topology[nodes[candidate.First]].Add(nodes[candidate.Second]);
            _topology[nodes[candidate.Second]].Add(nodes[candidate.First]);
            components.Union(candidate.First, candidate.Second);
            if (components.ComponentCount == 1) return;
        }
    }

    private void AddLocalVisibilityConnections(IReadOnlyList<LSVector2> nodes) {
        var seen = new HashSet<(int First, int Second)>();
        for (int first = 0; first < nodes.Count; first++) {
            var nearest = Enumerable.Range(0, nodes.Count)
                .Where(second => second != first)
                .OrderBy(second => (nodes[first] - nodes[second]).LengthSquared())
                .Take(LocalVisibilityNeighborCount);
            foreach (int second in nearest) {
                var edge = first < second ? (first, second) : (second, first);
                if (!seen.Add(edge) || _topology[nodes[edge.Item1]].Contains(nodes[edge.Item2])) continue;
                _visibilityTests++;
                if (!CanTraverse(nodes[edge.Item1], nodes[edge.Item2])) continue;
                _visibleConnections++;
                _topology[nodes[edge.Item1]].Add(nodes[edge.Item2]);
                _topology[nodes[edge.Item2]].Add(nodes[edge.Item1]);
            }
        }
    }

    public List<LSVector2> FindPath(LSVector2 start, LSVector2 goal) {
        if (!IsCurrent) throw new InvalidOperationException("Navigation mesh is stale. Rebuild it after changing the terrain world.");
        var resolvedStart = FindNearestWalkable(start);
        var resolvedGoal = FindNearestWalkable(goal);
        var graph = new PathQueryGraph(this, resolvedStart, resolvedGoal);
        if (!graph.HasNode(graph.Start) || !graph.HasNode(graph.Goal)) return new List<LSVector2>();
        var vertexPath = GraphAlgorithms.AStar(graph, graph.Start, graph.Goal,
            (from, to) => from.DistanceTo(to) * _settings.MinimumCost, GetTravelCost);
        vertexPath = OptimizePath(vertexPath);

        if (GetDynamicObstacles().Any()) return vertexPath;
        var funnelPath = FindFunnelPath(resolvedStart, resolvedGoal);
        if (funnelPath.Count == 0) return vertexPath;
        return vertexPath.Count == 0 || GetPathCost(funnelPath) <= GetPathCost(vertexPath) + Epsilon
            ? funnelPath
            : vertexPath;
    }

    private List<LSVector2> FindFunnelPath(LSVector2 start, LSVector2 goal) {
        int startTriangle = FindContainingTriangle(start);
        int goalTriangle = FindContainingTriangle(goal);
        if (startTriangle < 0 || goalTriangle < 0) return new List<LSVector2>();
        if (startTriangle == goalTriangle) return CanTraverse(start, goal)
            ? new List<LSVector2> { start, goal }
            : new List<LSVector2>();

        var graph = new TriangleGraph(_triangleNeighbors);
        var trianglePath = GraphAlgorithms.AStar(
            graph,
            startTriangle,
            goalTriangle,
            (from, _) => TriangleCentroid(from).DistanceTo(goal) * _settings.MinimumCost,
            GetTriangleTransitionCost);
        if (trianglePath.Count == 0) return new List<LSVector2>();

        var portals = new List<TerrainNavigationEdge> { new(start, start) };
        for (int index = 0; index < trianglePath.Count - 1; index++) {
            int from = trianglePath[index];
            int to = trianglePath[index + 1];
            var key = from < to ? (from, to) : (to, from);
            if (!_trianglePortals.TryGetValue(key, out var portal)) return new List<LSVector2>();
            portals.Add(OrientPortal(portal, TriangleCentroid(from), TriangleCentroid(to)));
        }
        portals.Add(new TerrainNavigationEdge(goal, goal));

        var path = RunFunnel(portals);
        return PathIsTraversable(path) ? path : new List<LSVector2>();
    }

    private int FindContainingTriangle(LSVector2 point) {
        for (int index = 0; index < _navigationTriangles.Count; index++) {
            var triangle = _navigationTriangles[index];
            if (PointIsInTriangle(point, Nodes[triangle.A], Nodes[triangle.B], Nodes[triangle.C])) return index;
        }
        return -1;
    }

    private LSVector2 TriangleCentroid(int triangleIndex) {
        return _navigationTriangles[triangleIndex].Centroid(Nodes);
    }

    private float GetTriangleTransitionCost(int from, int to) {
        var key = from < to ? (from, to) : (to, from);
        if (!_trianglePortals.TryGetValue(key, out var portal)) return float.PositiveInfinity;
        var midpoint = (portal.From + portal.To) * 0.5f;
        return TriangleCentroid(from).DistanceTo(midpoint) * _triangleCosts[from]
            + midpoint.DistanceTo(TriangleCentroid(to)) * _triangleCosts[to];
    }

    private static TerrainNavigationEdge OrientPortal(TerrainNavigationEdge portal, LSVector2 from, LSVector2 to) {
        var direction = to - from;
        var midpoint = (portal.From + portal.To) * 0.5f;
        return direction.Cross(portal.From - midpoint) >= 0f
            ? portal
            : new TerrainNavigationEdge(portal.To, portal.From);
    }

    private static List<LSVector2> RunFunnel(IReadOnlyList<TerrainNavigationEdge> portals) {
        var result = new List<LSVector2> { portals[0].From };
        var apex = portals[0].From;
        var left = portals[0].From;
        var right = portals[0].To;
        int apexIndex = 0, leftIndex = 0, rightIndex = 0;

        for (int index = 1; index < portals.Count; index++) {
            var newLeft = portals[index].From;
            var newRight = portals[index].To;

            if (TriangleArea2(apex, right, newRight) <= 0f) {
                if (apex == right || TriangleArea2(apex, left, newRight) > 0f) {
                    right = newRight;
                    rightIndex = index;
                } else {
                    result.Add(left);
                    apex = left;
                    apexIndex = leftIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    index = apexIndex;
                    continue;
                }
            }

            if (TriangleArea2(apex, left, newLeft) >= 0f) {
                if (apex == left || TriangleArea2(apex, right, newLeft) < 0f) {
                    left = newLeft;
                    leftIndex = index;
                } else {
                    result.Add(right);
                    apex = right;
                    apexIndex = rightIndex;
                    left = apex;
                    right = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    index = apexIndex;
                }
            }
        }

        var goal = portals[^1].From;
        if (result[^1] != goal) result.Add(goal);
        return result;
    }

    private bool PathIsTraversable(IReadOnlyList<LSVector2> path) {
        for (int index = 0; index < path.Count - 1; index++) {
            if (!CanTraverse(path[index], path[index + 1])) return false;
        }
        return true;
    }

    private float GetPathCost(IReadOnlyList<LSVector2> path) {
        float cost = 0f;
        for (int index = 0; index < path.Count - 1; index++) cost += GetTravelCost(path[index], path[index + 1]);
        return cost;
    }

    private static bool PointIsInTriangle(LSVector2 point, LSVector2 a, LSVector2 b, LSVector2 c) {
        float ab = (b - a).Cross(point - a);
        float bc = (c - b).Cross(point - b);
        float ca = (a - c).Cross(point - c);
        bool hasNegative = ab < -Epsilon || bc < -Epsilon || ca < -Epsilon;
        bool hasPositive = ab > Epsilon || bc > Epsilon || ca > Epsilon;
        return !(hasNegative && hasPositive);
    }

    private static float TriangleArea2(LSVector2 a, LSVector2 b, LSVector2 c) {
        return (b - a).Cross(c - a);
    }

    private List<LSVector2> OptimizePath(IReadOnlyList<LSVector2> path) {
        if (path.Count <= 2) return path.ToList();

        var costs = Enumerable.Repeat(float.PositiveInfinity, path.Count).ToArray();
        var previous = Enumerable.Repeat(-1, path.Count).ToArray();
        costs[0] = 0f;

        for (int to = 1; to < path.Count; to++) {
            for (int from = 0; from < to; from++) {
                if (float.IsPositiveInfinity(costs[from]) || !CanTraverse(path[from], path[to])) continue;
                float candidateCost = costs[from] + GetTravelCost(path[from], path[to]);
                if (candidateCost >= costs[to]) continue;
                costs[to] = candidateCost;
                previous[to] = from;
            }
        }

        if (previous[^1] < 0) return path.ToList();
        var optimized = new List<LSVector2>();
        for (int current = path.Count - 1; current >= 0; current = previous[current]) {
            optimized.Add(path[current]);
            if (current == 0) break;
        }
        optimized.Reverse();
        return optimized;
    }

    private sealed class TriangleGraph : IGraph<int> {
        private readonly IReadOnlyDictionary<int, List<int>> _neighbors;
        public IEnumerable<int> Nodes => _neighbors.Keys;

        public TriangleGraph(IReadOnlyDictionary<int, List<int>> neighbors) {
            _neighbors = neighbors;
        }

        public bool HasNode(int node) => _neighbors.ContainsKey(node);
        public IEnumerable<int> GetNeighbors(int node) => _neighbors.TryGetValue(node, out var neighbors) ? neighbors : Enumerable.Empty<int>();
    }

    private sealed class PathQueryGraph : IGraph<LSVector2> {
        private readonly Dictionary<LSVector2, List<LSVector2>> _neighbors;
        public LSVector2 Start { get; }
        public LSVector2 Goal { get; }
        public IEnumerable<LSVector2> Nodes => _neighbors.Keys;

        public PathQueryGraph(TerrainNavigationMesh<TTerrainType, TContentType> mesh, LSVector2 start, LSVector2 goal) {
            Start = start;
            Goal = goal;
            var dynamicObstacles = mesh.GetDynamicObstacles().ToList();
            _neighbors = dynamicObstacles.Count == 0
                ? mesh._topology.ToDictionary(pair => pair.Key, pair => new List<LSVector2>(pair.Value))
                : BuildDynamicTopology(mesh);
            foreach (var obstacle in dynamicObstacles) {
                foreach (var vertex in mesh.GetClearanceArcVertices(obstacle)) AddDynamicNode(mesh, vertex);
            }
            AddEndpoint(mesh, Start);
            AddEndpoint(mesh, Goal);
            if (_neighbors.ContainsKey(Start) && _neighbors.ContainsKey(Goal) && Start != Goal && mesh.CanTraverse(Start, Goal)) {
                _neighbors[Start].Add(Goal);
                _neighbors[Goal].Add(Start);
            }
        }

        public bool HasNode(LSVector2 node) => _neighbors.ContainsKey(node);
        public IEnumerable<LSVector2> GetNeighbors(LSVector2 node) => _neighbors.TryGetValue(node, out var neighbors) ? neighbors : Enumerable.Empty<LSVector2>();

        private static Dictionary<LSVector2, List<LSVector2>> BuildDynamicTopology(TerrainNavigationMesh<TTerrainType, TContentType> mesh) {
            var neighbors = mesh._topology.Keys.ToDictionary(node => node, _ => new List<LSVector2>());
            var indices = mesh._topology.Keys.Select((node, index) => (node, index)).ToDictionary(pair => pair.node, pair => pair.index);
            foreach (var (node, staticNeighbors) in mesh._topology) {
                foreach (var neighbor in staticNeighbors) {
                    if (indices[node] >= indices[neighbor] || !mesh.CanTraverse(node, neighbor)) continue;
                    neighbors[node].Add(neighbor);
                    neighbors[neighbor].Add(node);
                }
            }
            return neighbors;
        }

        private void AddDynamicNode(TerrainNavigationMesh<TTerrainType, TContentType> mesh, LSVector2 node) {
            if (!mesh.IsWalkable(node) || _neighbors.ContainsKey(node)) return;
            var nearest = _neighbors.Keys
                .OrderBy(candidate => (candidate - node).LengthSquared())
                .Take(LocalVisibilityNeighborCount)
                .ToArray();
            _neighbors.Add(node, new List<LSVector2>());
            foreach (var candidate in nearest) {
                if (!mesh.CanTraverse(node, candidate)) continue;
                _neighbors[node].Add(candidate);
                _neighbors[candidate].Add(node);
            }
        }

        private void AddEndpoint(TerrainNavigationMesh<TTerrainType, TContentType> mesh, LSVector2 endpoint) {
            if (!mesh.IsWalkable(endpoint) || _neighbors.ContainsKey(endpoint)) return;
            _neighbors.Add(endpoint, new List<LSVector2>());
            foreach (var node in mesh._topology.Keys) {
                if (!mesh.CanTraverse(endpoint, node)) continue;
                _neighbors[endpoint].Add(node);
                _neighbors[node].Add(endpoint);
            }
        }
    }

    private IEnumerable<Polygon2D> GetObstacles() {
        foreach (var patch in _world.Patches) {
            if (_settings.GetTerrainCost(patch) <= 0f) yield return RequireConvexPolygon(patch.Shape, "terrain patch");
        }
        foreach (var content in _world.Contents) {
            if (_settings.BlocksContent(content) && content.Mobility == TerrainContentMobility.Static) yield return RequireConvexPolygon(content.Shape, "terrain content");
        }
    }

    private IEnumerable<Polygon2D> GetDynamicObstacles() {
        foreach (var content in _world.Contents) {
            if (_settings.BlocksContent(content) && content.Mobility == TerrainContentMobility.Dynamic) {
                yield return RequireConvexPolygon(content.Shape, "dynamic terrain content");
            }
        }
    }

    private static Polygon2D RequirePolygon(IShape2D shape, string source) {
        if (shape is not Polygon2D polygon) throw new LSArgumentException($"Navigation requires Polygon2D shapes; {source} uses {shape.GetType().Name}.");
        return polygon;
    }

    private static Polygon2D RequireConvexPolygon(IShape2D shape, string source) {
        var polygon = RequirePolygon(shape, source);
        if (!polygon.IsConvex) throw new LSArgumentException($"Navigation requires convex Polygon2D obstacles; decompose concave {source} before pathfinding.");
        return polygon;
    }

    private IEnumerable<LSVector2> GetClearanceArcVertices(Polygon2D polygon) {
        for (int index = 0; index < polygon.Vertices.Count; index++) {
            var previous = polygon.Vertices[(index + polygon.Vertices.Count - 1) % polygon.Vertices.Count];
            var current = polygon.Vertices[index];
            var next = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
            var previousNormal = OutwardNormal(previous, current, polygon.IsClockwise);
            var nextNormal = OutwardNormal(current, next, polygon.IsClockwise);
            float startAngle = MathF.Atan2(previousNormal.Y, previousNormal.X);
            float endAngle = MathF.Atan2(nextNormal.Y, nextNormal.X);
            if (!polygon.IsClockwise) while (endAngle < startAngle) endAngle += MathF.Tau;
            else while (endAngle > startAngle) endAngle -= MathF.Tau;

            float stepAngle = MathF.Abs(endAngle - startAngle) / CornerArcSegments;
            float radius = (_settings.AgentRadius + Epsilon) / MathF.Cos(stepAngle * 0.5f) + Epsilon;
            for (int segment = 0; segment <= CornerArcSegments; segment++) {
                float weight = segment / (float)CornerArcSegments;
                yield return current + LSVector2.FromAngle(startAngle + (endAngle - startAngle) * weight) * radius;
            }
        }
    }

    private static LSVector2 OutwardNormal(LSVector2 from, LSVector2 to, bool isClockwise) {
        var edge = to - from;
        var normal = isClockwise ? new LSVector2(-edge.Y, edge.X) : new LSVector2(edge.Y, -edge.X);
        return new LSVector2(normal.Normalized());
    }

    private LSVector2 FindNearestWalkable(LSVector2 position) {
        if (IsWalkable(position)) return position;
        float step = MathF.Max(8f, _settings.AgentRadius * 2f + Epsilon);
        for (int radius = 1; radius <= 24; radius++) {
            for (int sample = 0; sample < 32; sample++) {
                var candidate = position + LSVector2.FromAngle(MathF.Tau * sample / 32f) * step * radius;
                if (IsWalkable(candidate)) return candidate;
            }
        }
        return position;
    }

    private bool IsWalkable(LSVector2 point) {
        if (!_world.Bounds.Contains(point.X, point.Y) || _settings.GetTerrainCost(_world.ResolvePatchAt(point.X, point.Y)) <= 0f) return false;
        var area = new Bounds(point.X, point.Y, _settings.AgentRadius * 2f + Epsilon, _settings.AgentRadius * 2f + Epsilon);
        return !HasObstacleWithin(area, obstacle => PointToPolygonDistance(point, obstacle) <= _settings.AgentRadius + Epsilon);
    }

    private bool CanTraverse(LSVector2 from, LSVector2 to) {
        if (!IsWalkable(from) || !IsWalkable(to)) return false;
        var area = SegmentBounds(from, to, _settings.AgentRadius + Epsilon);
        return !HasObstacleWithin(area, obstacle => SegmentToPolygonDistance(from, to, obstacle) <= _settings.AgentRadius + Epsilon)
            && !float.IsPositiveInfinity(GetTravelCost(from, to));
    }

    private float GetTravelCost(LSVector2 from, LSVector2 to) {
        float distance = from.DistanceTo(to);
        int samples = Math.Max(1, (int)MathF.Ceiling(distance / 16f));
        _terrainCostSamples += samples;
        float cost = 0f;
        var delta = to - from;
        for (int sample = 0; sample < samples; sample++) {
            var point = from + delta * ((sample + 0.5f) / samples);
            float terrainCost = _settings.GetTerrainCost(_world.ResolvePatchAt(point.X, point.Y));
            if (terrainCost <= 0f) return float.PositiveInfinity;
            cost += distance / samples * terrainCost;
        }
        return cost;
    }

    private IEnumerable<Polygon2D> GetObstacleCandidates(Bounds area) {
        foreach (var patch in _world.QueryPatches(area)) if (_settings.GetTerrainCost(patch) <= 0f) yield return RequireConvexPolygon(patch.Shape, "terrain patch");
        foreach (var content in _world.QueryContents(area)) {
            if (_settings.BlocksContent(content) && (!_buildingStaticBake || content.Mobility == TerrainContentMobility.Static)) {
                yield return RequireConvexPolygon(content.Shape, "terrain content");
            }
        }
    }

    private bool HasObstacleWithin(Bounds area, Func<Polygon2D, bool> predicate) {
        foreach (var obstacle in GetObstacleCandidates(area)) {
            _obstacleCandidateChecks++;
            if (predicate(obstacle)) return true;
        }
        return false;
    }

    private static Bounds SegmentBounds(LSVector2 from, LSVector2 to, float padding) {
        float minX = MathF.Min(from.X, to.X) - padding, maxX = MathF.Max(from.X, to.X) + padding;
        float minY = MathF.Min(from.Y, to.Y) - padding, maxY = MathF.Max(from.Y, to.Y) + padding;
        return new Bounds((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, maxX - minX, maxY - minY);
    }

    private static float PointToPolygonDistance(LSVector2 point, Polygon2D polygon) {
        if (polygon.Contains(point.X, point.Y)) return 0f;
        float distance = float.PositiveInfinity;
        for (int index = 0; index < polygon.Vertices.Count; index++) distance = MathF.Min(distance, PointToSegmentDistance(point, polygon.Vertices[index], polygon.Vertices[(index + 1) % polygon.Vertices.Count]));
        return distance;
    }

    private static float SegmentToPolygonDistance(LSVector2 from, LSVector2 to, Polygon2D polygon) {
        if (polygon.Contains(from.X, from.Y) || polygon.Contains(to.X, to.Y)) return 0f;
        float distance = float.PositiveInfinity;
        for (int index = 0; index < polygon.Vertices.Count; index++) {
            var a = polygon.Vertices[index]; var b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
            if (SegmentsIntersect(from, to, a, b)) return 0f;
            distance = MathF.Min(distance, SegmentToSegmentDistance(from, to, a, b));
        }
        return distance;
    }

    private static float PointToSegmentDistance(LSVector2 point, LSVector2 a, LSVector2 b) {
        var segment = b - a; float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= Epsilon) return point.DistanceTo(a);
        float t = Math.Clamp((point - a).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(a + segment * t);
    }

    private static float SegmentToSegmentDistance(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        return MathF.Min(MathF.Min(PointToSegmentDistance(a, c, d), PointToSegmentDistance(b, c, d)), MathF.Min(PointToSegmentDistance(c, a, b), PointToSegmentDistance(d, a, b)));
    }

    private static bool SegmentsIntersect(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        float abC = (b - a).Cross(c - a), abD = (b - a).Cross(d - a);
        float cdA = (d - c).Cross(a - c), cdB = (d - c).Cross(b - c);
        if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) && ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
        return MathF.Abs(abC) <= Epsilon && PointToSegmentDistance(c, a, b) <= Epsilon || MathF.Abs(abD) <= Epsilon && PointToSegmentDistance(d, a, b) <= Epsilon || MathF.Abs(cdA) <= Epsilon && PointToSegmentDistance(a, c, d) <= Epsilon || MathF.Abs(cdB) <= Epsilon && PointToSegmentDistance(b, c, d) <= Epsilon;
    }

    private readonly struct NavigationTriangle {
        public int A { get; }
        public int B { get; }
        public int C { get; }

        public NavigationTriangle(int a, int b, int c) {
            A = a;
            B = b;
            C = c;
        }

        public LSVector2 Centroid(IReadOnlyList<LSVector2> points) {
            return (points[A] + points[B] + points[C]) / 3f;
        }
    }

    private readonly record struct QuantizedPoint(long X, long Y) {
        private const double Scale = 10000d;

        public static QuantizedPoint From(LSVector2 point) {
            return new QuantizedPoint((long)Math.Round(point.X * Scale), (long)Math.Round(point.Y * Scale));
        }
    }

    private sealed class DisjointSet {
        private readonly int[] _parents;
        private readonly byte[] _ranks;
        public int ComponentCount { get; private set; }

        public DisjointSet(int count) {
            _parents = new int[count];
            _ranks = new byte[count];
            ComponentCount = count;
            for (int index = 0; index < count; index++) _parents[index] = index;
        }

        public int Find(int item) {
            while (_parents[item] != item) {
                _parents[item] = _parents[_parents[item]];
                item = _parents[item];
            }
            return item;
        }

        public void Union(int first, int second) {
            first = Find(first);
            second = Find(second);
            if (first == second) return;
            if (_ranks[first] < _ranks[second]) (first, second) = (second, first);
            _parents[second] = first;
            if (_ranks[first] == _ranks[second]) _ranks[first]++;
            ComponentCount--;
        }
    }
}
