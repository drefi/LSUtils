namespace LSUtils.Geometry.Triangulation;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>A segment that must be present in a constrained triangulation.</summary>
public readonly record struct TriangulationConstraint(LSVector2 From, LSVector2 To);

/// <summary>Indices of the three vertices of a counter-clockwise triangle.</summary>
public readonly record struct TriangulationTriangle(int A, int B, int C);

/// <summary>The vertices, triangles and noded constraints produced by a triangulation.</summary>
public sealed class ConstrainedTriangulationResult {
    internal ConstrainedTriangulationResult(
        IReadOnlyList<LSVector2> vertices,
        IReadOnlyList<TriangulationTriangle> triangles,
        IReadOnlyList<(int From, int To)> constraints) {
        Vertices = vertices;
        Triangles = triangles;
        Constraints = constraints;
    }

    public IReadOnlyList<LSVector2> Vertices { get; }
    public IReadOnlyList<TriangulationTriangle> Triangles { get; }
    public IReadOnlyList<(int From, int To)> Constraints { get; }
}

/// <summary>
/// Builds a planar constrained triangulation. Constraint intersections and
/// collinear overlaps are noded before the required edges are recovered.
/// </summary>
public static class ConstrainedTriangulation2D {
    private const double Epsilon = 1e-8;
    private const double QuantizationScale = 10000d;

    public static ConstrainedTriangulationResult Triangulate(IEnumerable<TriangulationConstraint> constraints) {
        if (constraints == null) throw new LSArgumentNullException(nameof(constraints));
        var input = constraints.Where(segment => segment.From != segment.To).ToList();
        if (input.Count == 0) return new ConstrainedTriangulationResult(Array.Empty<LSVector2>(), Array.Empty<TriangulationTriangle>(), Array.Empty<(int, int)>());

        var noded = NodeConstraints(input);
        var triangles = BuildDelaunay(noded.Vertices);
        RecoverConstraints(noded.Vertices, triangles, noded.Constraints);
        return new ConstrainedTriangulationResult(noded.Vertices.AsReadOnly(), triangles.AsReadOnly(), noded.Constraints.AsReadOnly());
    }

    private static NodedConstraints NodeConstraints(IReadOnlyList<TriangulationConstraint> segments) {
        var splitParameters = Enumerable.Range(0, segments.Count)
            .Select(_ => new List<double> { 0d, 1d })
            .ToArray();

        for (int first = 0; first < segments.Count; first++) {
            for (int second = first + 1; second < segments.Count; second++) {
                AddIntersectionSplits(segments[first], splitParameters[first], segments[second], splitParameters[second]);
            }
        }

        var vertices = new List<LSVector2>();
        var vertexIndices = new Dictionary<QuantizedPoint, int>();
        var constraints = new List<(int From, int To)>();
        var seenConstraints = new HashSet<(int From, int To)>();
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++) {
            var segment = segments[segmentIndex];
            var parameters = splitParameters[segmentIndex]
                .Select(ClampParameter)
                .Distinct(new ParameterComparer())
                .OrderBy(value => value)
                .ToList();
            for (int index = 0; index < parameters.Count - 1; index++) {
                var fromPoint = Interpolate(segment, parameters[index]);
                var toPoint = Interpolate(segment, parameters[index + 1]);
                int from = GetOrAddVertex(fromPoint, vertices, vertexIndices);
                int to = GetOrAddVertex(toPoint, vertices, vertexIndices);
                if (from == to) continue;
                var edge = NormalizeEdge(from, to);
                if (seenConstraints.Add(edge)) constraints.Add(edge);
            }
        }
        return new NodedConstraints(vertices, constraints);
    }

    private static void AddIntersectionSplits(
        TriangulationConstraint first,
        List<double> firstSplits,
        TriangulationConstraint second,
        List<double> secondSplits) {
        var r = first.To - first.From;
        var s = second.To - second.From;
        double denominator = Cross(r, s);
        var offset = second.From - first.From;

        if (Math.Abs(denominator) > Epsilon) {
            double firstT = Cross(offset, s) / denominator;
            double secondT = Cross(offset, r) / denominator;
            if (IsOnSegment(firstT) && IsOnSegment(secondT)) {
                firstSplits.Add(firstT);
                secondSplits.Add(secondT);
            }
            return;
        }
        if (Math.Abs(Cross(offset, r)) > Epsilon) return;

        AddCollinearEndpoint(first.From, second, 0d, firstSplits, secondSplits);
        AddCollinearEndpoint(first.To, second, 1d, firstSplits, secondSplits);
        AddCollinearEndpoint(second.From, first, 0d, secondSplits, firstSplits);
        AddCollinearEndpoint(second.To, first, 1d, secondSplits, firstSplits);
    }

    private static void AddCollinearEndpoint(
        LSVector2 point,
        TriangulationConstraint containing,
        double endpointParameter,
        List<double> endpointSplits,
        List<double> containingSplits) {
        double parameter = ProjectParameter(point, containing);
        if (!IsOnSegment(parameter)) return;
        endpointSplits.Add(endpointParameter);
        containingSplits.Add(parameter);
    }

    private static List<TriangulationTriangle> BuildDelaunay(IReadOnlyList<LSVector2> inputVertices) {
        if (inputVertices.Count < 3) return new List<TriangulationTriangle>();
        var points = inputVertices.ToList();
        float minX = points.Min(point => point.X), maxX = points.Max(point => point.X);
        float minY = points.Min(point => point.Y), maxY = points.Max(point => point.Y);
        float span = MathF.Max(maxX - minX, maxY - minY);
        if (span <= float.Epsilon) return new List<TriangulationTriangle>();
        float centerX = (minX + maxX) * 0.5f, centerY = (minY + maxY) * 0.5f;
        int firstSuperVertex = points.Count;
        points.Add(new LSVector2(centerX - span * 32f, centerY - span * 2f));
        points.Add(new LSVector2(centerX, centerY + span * 32f));
        points.Add(new LSVector2(centerX + span * 32f, centerY - span * 2f));

        var triangles = new List<TriangulationTriangle> { MakeCounterClockwise(firstSuperVertex, firstSuperVertex + 1, firstSuperVertex + 2, points) };
        for (int pointIndex = 0; pointIndex < firstSuperVertex; pointIndex++) {
            var badTriangleIndices = new List<int>();
            var boundaryEdges = new Dictionary<(int From, int To), int>();
            for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++) {
                if (!CircumcircleContains(triangles[triangleIndex], points, points[pointIndex])) continue;
                badTriangleIndices.Add(triangleIndex);
                foreach (var edge in GetEdges(triangles[triangleIndex])) boundaryEdges[edge] = boundaryEdges.TryGetValue(edge, out int count) ? count + 1 : 1;
            }
            for (int index = badTriangleIndices.Count - 1; index >= 0; index--) triangles.RemoveAt(badTriangleIndices[index]);
            foreach (var (edge, count) in boundaryEdges) {
                if (count != 1 || Math.Abs(Orientation(points[edge.From], points[edge.To], points[pointIndex])) <= Epsilon) continue;
                triangles.Add(MakeCounterClockwise(edge.From, edge.To, pointIndex, points));
            }
        }

        triangles.RemoveAll(triangle => triangle.A >= firstSuperVertex || triangle.B >= firstSuperVertex || triangle.C >= firstSuperVertex);
        return triangles;
    }

    private static void RecoverConstraints(
        IReadOnlyList<LSVector2> vertices,
        List<TriangulationTriangle> triangles,
        IReadOnlyList<(int From, int To)> constraints) {
        var recovered = new HashSet<(int From, int To)>();
        var owners = BuildEdgeOwners(triangles);
        foreach (var constraint in constraints) {
            int iterationLimit = Math.Max(32, triangles.Count * 8);
            for (int iteration = 0; iteration < iterationLimit; iteration++) {
                if (owners.ContainsKey(constraint)) {
                    recovered.Add(constraint);
                    break;
                }
                // A valid constrained boundary may be represented by several
                // collinear triangulation edges when another boundary ends on
                // it. Treat that chain as recovered instead of trying to force
                // a non-existent long edge through intermediate vertices.
                if (HasCollinearEdgeChain(constraint, vertices, owners)) {
                    recovered.Add(constraint);
                    break;
                }

                (int From, int To)? candidate = null;
                int[]? candidateOwners = null;
                foreach (var (edge, edgeOwners) in owners) {
                    if (edgeOwners.Count != 2 || recovered.Contains(edge)) continue;
                    if (!ProperlyIntersects(vertices[constraint.From], vertices[constraint.To], vertices[edge.From], vertices[edge.To])) continue;
                    if (!CanFlipEdge(edge, edgeOwners, constraint, vertices, triangles, recovered)) continue;
                    candidate = edge;
                    candidateOwners = edgeOwners.ToArray();
                    break;
                }
                if (candidate == null || candidateOwners == null) {
                    if (!RecoverConstraintCavity(constraint, vertices, triangles, out _)
                        && !RecoverConstraintWithFlipSearch(constraint, vertices, triangles, recovered)) {
                        int fromDegree = owners.Keys.Count(edge => edge.From == constraint.From || edge.To == constraint.From);
                        int toDegree = owners.Keys.Count(edge => edge.From == constraint.To || edge.To == constraint.To);
                        var pointsOnSegment = Enumerable.Range(0, vertices.Count)
                            .Where(index => index != constraint.From && index != constraint.To
                                && PointOnSegment(vertices[index], vertices[constraint.From], vertices[constraint.To]))
                            .ToList();
                        int crossings = CountConstraintCrossings(constraint, vertices, owners);
                        throw new LSException($"Could not recover triangulation constraint {constraint.From}-{constraint.To}; endpoint degrees are {fromDegree}/{toDegree}, crossings={crossings}, intermediate vertices={string.Join(',', pointsOnSegment)}.");
                    }
                    owners = BuildEdgeOwners(triangles);
                    continue;
                }
                FlipEdge(candidate.Value, candidateOwners, vertices, triangles, owners);
            }
            if (!recovered.Contains(constraint)) throw new LSException($"Constraint recovery exceeded its iteration limit for {constraint.From}-{constraint.To}.");
        }
    }

    private static bool HasCollinearEdgeChain(
        (int From, int To) constraint,
        IReadOnlyList<LSVector2> vertices,
        IReadOnlyDictionary<(int From, int To), List<int>> owners) {
        var adjacency = new Dictionary<int, List<int>>();
        foreach (var edge in owners.Keys) {
            if (!PointOnSegment(vertices[edge.From], vertices[constraint.From], vertices[constraint.To])
                || !PointOnSegment(vertices[edge.To], vertices[constraint.From], vertices[constraint.To])) continue;
            if (!adjacency.TryGetValue(edge.From, out var fromNeighbors))
                adjacency.Add(edge.From, fromNeighbors = new List<int>());
            if (!adjacency.TryGetValue(edge.To, out var toNeighbors))
                adjacency.Add(edge.To, toNeighbors = new List<int>());
            fromNeighbors.Add(edge.To);
            toNeighbors.Add(edge.From);
        }
        if (!adjacency.ContainsKey(constraint.From) || !adjacency.ContainsKey(constraint.To)) return false;

        var pending = new Queue<int>();
        var visited = new HashSet<int> { constraint.From };
        pending.Enqueue(constraint.From);
        while (pending.Count > 0) {
            int current = pending.Dequeue();
            if (current == constraint.To) return true;
            foreach (int next in adjacency[current]) {
                if (visited.Add(next)) pending.Enqueue(next);
            }
        }
        return false;
    }

    private static bool RecoverConstraintWithFlipSearch(
        (int From, int To) constraint,
        IReadOnlyList<LSVector2> vertices,
        List<TriangulationTriangle> triangles,
        IReadOnlySet<(int From, int To)> recovered) {
        const int stateLimit = 1000;
        var pending = new PriorityQueue<List<TriangulationTriangle>, int>();
        var visited = new HashSet<string>();
        var initial = triangles.ToList();
        pending.Enqueue(initial, CountConstraintCrossings(constraint, vertices, BuildEdgeOwners(initial)));
        visited.Add(GetTriangulationKey(initial));

        for (int stateCount = 0; pending.Count > 0 && stateCount < stateLimit; stateCount++) {
            var state = pending.Dequeue();
            var stateOwners = BuildEdgeOwners(state);
            if (stateOwners.ContainsKey(constraint)) {
                triangles.Clear();
                triangles.AddRange(state);
                return true;
            }

            var crossingEdges = stateOwners.Keys
                .Where(edge => ProperlyIntersects(vertices[constraint.From], vertices[constraint.To], vertices[edge.From], vertices[edge.To]))
                .ToHashSet();
            var localTriangles = crossingEdges.SelectMany(edge => stateOwners[edge]).ToHashSet();
            var candidateEdges = crossingEdges
                .Concat(localTriangles.SelectMany(index => GetEdges(state[index])))
                .Distinct()
                .ToList();
            foreach (var edge in candidateEdges) {
                if (!stateOwners.TryGetValue(edge, out var edgeOwners)) continue;
                if (edgeOwners.Count != 2 || recovered.Contains(edge)) continue;
                if (!CanFlipEdge(edge, edgeOwners, constraint, vertices, state, recovered, requireProgress: false)) continue;
                var next = state.ToList();
                var nextOwners = BuildEdgeOwners(next);
                FlipEdge(edge, nextOwners[edge].ToArray(), vertices, next, nextOwners);
                string key = GetTriangulationKey(next);
                if (!visited.Add(key)) continue;
                pending.Enqueue(next, CountConstraintCrossings(constraint, vertices, nextOwners));
            }
        }
        return false;
    }

    private static int CountConstraintCrossings(
        (int From, int To) constraint,
        IReadOnlyList<LSVector2> vertices,
        IReadOnlyDictionary<(int From, int To), List<int>> owners) {
        return owners.Keys.Count(edge => ProperlyIntersects(
            vertices[constraint.From], vertices[constraint.To], vertices[edge.From], vertices[edge.To]));
    }

    private static string GetTriangulationKey(IReadOnlyList<TriangulationTriangle> triangles) {
        return string.Join(";", triangles
            .Select(triangle => new[] { triangle.A, triangle.B, triangle.C }.OrderBy(value => value).ToArray())
            .OrderBy(values => values[0]).ThenBy(values => values[1]).ThenBy(values => values[2])
            .Select(values => $"{values[0]},{values[1]},{values[2]}"));
    }

    private static bool RecoverConstraintCavity(
        (int From, int To) constraint,
        IReadOnlyList<LSVector2> vertices,
        List<TriangulationTriangle> triangles,
        out string failure) {
        var currentOwners = BuildEdgeOwners(triangles);
        var cavity = currentOwners
            .Where(pair => ProperlyIntersects(
                vertices[constraint.From], vertices[constraint.To],
                vertices[pair.Key.From], vertices[pair.Key.To]))
            .SelectMany(pair => pair.Value)
            .ToHashSet();
        if (cavity.Count == 0) {
            failure = "no intersected cavity triangles";
            return false;
        }

        var edgeCounts = new Dictionary<(int From, int To), int>();
        foreach (int triangleIndex in cavity) {
            foreach (var edge in GetEdges(triangles[triangleIndex])) {
                edgeCounts[edge] = edgeCounts.TryGetValue(edge, out int count) ? count + 1 : 1;
            }
        }
        var boundaryEdges = edgeCounts.Where(pair => pair.Value == 1).Select(pair => pair.Key).ToList();
        var boundaryNeighbors = new Dictionary<int, List<int>>();
        foreach (var edge in boundaryEdges) {
            AddNeighbor(boundaryNeighbors, edge.From, edge.To);
            AddNeighbor(boundaryNeighbors, edge.To, edge.From);
        }
        var cavityVertices = cavity.SelectMany(index => new[] { triangles[index].A, triangles[index].B, triangles[index].C }).ToHashSet();
        var internalVertices = cavityVertices.Where(vertex => !boundaryNeighbors.ContainsKey(vertex)).ToList();
        if (internalVertices.Count > 0) {
            failure = $"cavity contains internal vertices {string.Join(',', internalVertices)}";
            return false;
        }
        if (!boundaryNeighbors.TryGetValue(constraint.From, out var startNeighbors) || startNeighbors.Count != 2
            || !boundaryNeighbors.TryGetValue(constraint.To, out var goalNeighbors) || goalNeighbors.Count != 2) {
            int startDegree = boundaryNeighbors.TryGetValue(constraint.From, out var starts) ? starts.Count : 0;
            int goalDegree = boundaryNeighbors.TryGetValue(constraint.To, out var goals) ? goals.Count : 0;
            failure = $"cavity={cavity.Count}, boundary={boundaryEdges.Count}, endpoint degrees={startDegree}/{goalDegree}";
            return false;
        }

        var firstPath = WalkBoundary(constraint.From, constraint.To, startNeighbors[0], boundaryNeighbors);
        var secondPath = WalkBoundary(constraint.From, constraint.To, startNeighbors[1], boundaryNeighbors);
        if (firstPath.Count < 2 || secondPath.Count < 2) {
            failure = $"boundary walk failed with path sizes {firstPath.Count}/{secondPath.Count}";
            return false;
        }
        var replacements = TriangulatePolygon(firstPath, vertices);
        replacements.AddRange(TriangulatePolygon(secondPath, vertices));
        if (replacements.Count == 0) {
            failure = $"ear clipping failed for path sizes {firstPath.Count}/{secondPath.Count}";
            return false;
        }

        foreach (int triangleIndex in cavity.OrderByDescending(index => index)) triangles.RemoveAt(triangleIndex);
        triangles.AddRange(replacements);
        bool recovered = BuildEdgeOwners(triangles).ContainsKey(constraint);
        failure = recovered ? string.Empty : "replacement triangles omitted the constraint";
        return recovered;
    }

    private static List<int> WalkBoundary(int start, int goal, int firstNeighbor, IReadOnlyDictionary<int, List<int>> neighbors) {
        var path = new List<int> { start };
        int previous = start, current = firstNeighbor;
        int limit = neighbors.Count + 1;
        while (path.Count <= limit) {
            path.Add(current);
            if (current == goal) return path;
            if (!neighbors.TryGetValue(current, out var candidates) || candidates.Count != 2) return new List<int>();
            int next = candidates[0] == previous ? candidates[1] : candidates[0];
            previous = current;
            current = next;
        }
        return new List<int>();
    }

    private static List<TriangulationTriangle> TriangulatePolygon(IReadOnlyList<int> polygon, IReadOnlyList<LSVector2> vertices) {
        var remaining = polygon.ToList();
        if (SignedArea(remaining, vertices) < 0d) remaining.Reverse();
        var triangles = new List<TriangulationTriangle>();
        int iterationLimit = remaining.Count * remaining.Count;
        for (int iteration = 0; remaining.Count > 3 && iteration < iterationLimit; iteration++) {
            bool clipped = false;
            for (int index = 0; index < remaining.Count; index++) {
                int previous = remaining[(index + remaining.Count - 1) % remaining.Count];
                int current = remaining[index];
                int next = remaining[(index + 1) % remaining.Count];
                if (Orientation(vertices[previous], vertices[current], vertices[next]) <= Epsilon) continue;
                if (remaining.Any(point => point != previous && point != current && point != next
                    && (PointStrictlyInsideTriangle(vertices[point], vertices[previous], vertices[current], vertices[next])
                        || PointOnSegment(vertices[point], vertices[previous], vertices[next])))) continue;
                triangles.Add(new TriangulationTriangle(previous, current, next));
                remaining.RemoveAt(index);
                clipped = true;
                break;
            }
            if (!clipped) return new List<TriangulationTriangle>();
        }
        if (remaining.Count == 3 && Math.Abs(Orientation(vertices[remaining[0]], vertices[remaining[1]], vertices[remaining[2]])) > Epsilon) {
            triangles.Add(MakeCounterClockwise(remaining[0], remaining[1], remaining[2], vertices));
        }
        return triangles;
    }

    private static bool PointStrictlyInsideTriangle(LSVector2 point, LSVector2 a, LSVector2 b, LSVector2 c) {
        return Orientation(a, b, point) > Epsilon
            && Orientation(b, c, point) > Epsilon
            && Orientation(c, a, point) > Epsilon;
    }

    private static bool PointOnSegment(LSVector2 point, LSVector2 from, LSVector2 to) {
        if (Math.Abs(Orientation(from, to, point)) > Epsilon) return false;
        return point.X >= Math.Min(from.X, to.X) - Epsilon && point.X <= Math.Max(from.X, to.X) + Epsilon
            && point.Y >= Math.Min(from.Y, to.Y) - Epsilon && point.Y <= Math.Max(from.Y, to.Y) + Epsilon;
    }

    private static double SignedArea(IReadOnlyList<int> polygon, IReadOnlyList<LSVector2> vertices) {
        double area = 0d;
        for (int index = 0; index < polygon.Count; index++) {
            var current = vertices[polygon[index]];
            var next = vertices[polygon[(index + 1) % polygon.Count]];
            area += (double)current.X * next.Y - (double)next.X * current.Y;
        }
        return area * 0.5d;
    }

    private static void AddNeighbor(Dictionary<int, List<int>> neighbors, int from, int to) {
        if (!neighbors.TryGetValue(from, out var values)) neighbors.Add(from, values = new List<int>(2));
        values.Add(to);
    }

    private static bool CanFlipEdge(
        (int From, int To) edge,
        IReadOnlyList<int> owners,
        (int From, int To) activeConstraint,
        IReadOnlyList<LSVector2> vertices,
        List<TriangulationTriangle> triangles,
        IReadOnlySet<(int From, int To)> recovered,
        bool requireProgress = true) {
        int firstOpposite = OppositeVertex(triangles[owners[0]], edge);
        int secondOpposite = OppositeVertex(triangles[owners[1]], edge);
        var replacement = NormalizeEdge(firstOpposite, secondOpposite);
        if (replacement.From == replacement.To || recovered.Contains(replacement)) return false;
        if (!ProperlyIntersects(vertices[edge.From], vertices[edge.To], vertices[firstOpposite], vertices[secondOpposite])) return false;
        if (requireProgress && !SharesEndpoint(replacement, activeConstraint)
            && ProperlyIntersects(vertices[replacement.From], vertices[replacement.To], vertices[activeConstraint.From], vertices[activeConstraint.To])) return false;
        foreach (var constraint in recovered) {
            if (SharesEndpoint(replacement, constraint)) continue;
            if (ProperlyIntersects(vertices[replacement.From], vertices[replacement.To], vertices[constraint.From], vertices[constraint.To])) return false;
        }
        return true;
    }

    private static void FlipEdge(
        (int From, int To) edge,
        IReadOnlyList<int> edgeOwners,
        IReadOnlyList<LSVector2> vertices,
        List<TriangulationTriangle> triangles,
        Dictionary<(int From, int To), List<int>> owners) {
        int firstTriangle = edgeOwners[0], secondTriangle = edgeOwners[1];
        var first = triangles[firstTriangle];
        var second = triangles[secondTriangle];
        int firstOpposite = OppositeVertex(first, edge);
        int secondOpposite = OppositeVertex(second, edge);
        RemoveTriangleOwner(owners, first, firstTriangle);
        RemoveTriangleOwner(owners, second, secondTriangle);
        triangles[firstTriangle] = MakeCounterClockwise(firstOpposite, secondOpposite, edge.From, vertices);
        triangles[secondTriangle] = MakeCounterClockwise(secondOpposite, firstOpposite, edge.To, vertices);
        AddTriangleOwner(owners, triangles[firstTriangle], firstTriangle);
        AddTriangleOwner(owners, triangles[secondTriangle], secondTriangle);
    }

    private static Dictionary<(int From, int To), List<int>> BuildEdgeOwners(IReadOnlyList<TriangulationTriangle> triangles) {
        var owners = new Dictionary<(int From, int To), List<int>>();
        for (int index = 0; index < triangles.Count; index++) AddTriangleOwner(owners, triangles[index], index);
        return owners;
    }

    private static void AddTriangleOwner(Dictionary<(int From, int To), List<int>> owners, TriangulationTriangle triangle, int triangleIndex) {
        foreach (var edge in GetEdges(triangle)) {
            if (!owners.TryGetValue(edge, out var edgeOwners)) owners.Add(edge, edgeOwners = new List<int>(2));
            edgeOwners.Add(triangleIndex);
        }
    }

    private static void RemoveTriangleOwner(Dictionary<(int From, int To), List<int>> owners, TriangulationTriangle triangle, int triangleIndex) {
        foreach (var edge in GetEdges(triangle)) {
            var edgeOwners = owners[edge];
            edgeOwners.Remove(triangleIndex);
            if (edgeOwners.Count == 0) owners.Remove(edge);
        }
    }

    private static IEnumerable<(int From, int To)> GetEdges(TriangulationTriangle triangle) {
        yield return NormalizeEdge(triangle.A, triangle.B);
        yield return NormalizeEdge(triangle.B, triangle.C);
        yield return NormalizeEdge(triangle.C, triangle.A);
    }

    private static TriangulationTriangle MakeCounterClockwise(int a, int b, int c, IReadOnlyList<LSVector2> vertices) {
        return Orientation(vertices[a], vertices[b], vertices[c]) >= 0d
            ? new TriangulationTriangle(a, b, c)
            : new TriangulationTriangle(a, c, b);
    }

    private static bool CircumcircleContains(TriangulationTriangle triangle, IReadOnlyList<LSVector2> vertices, LSVector2 point) {
        var a = vertices[triangle.A];
        var b = vertices[triangle.B];
        var c = vertices[triangle.C];
        double ax = a.X - point.X, ay = a.Y - point.Y;
        double bx = b.X - point.X, by = b.Y - point.Y;
        double cx = c.X - point.X, cy = c.Y - point.Y;
        double determinant = (ax * ax + ay * ay) * (bx * cy - by * cx)
            - (bx * bx + by * by) * (ax * cy - ay * cx)
            + (cx * cx + cy * cy) * (ax * by - ay * bx);
        return determinant > Epsilon;
    }

    private static int OppositeVertex(TriangulationTriangle triangle, (int From, int To) edge) {
        if (triangle.A != edge.From && triangle.A != edge.To) return triangle.A;
        if (triangle.B != edge.From && triangle.B != edge.To) return triangle.B;
        return triangle.C;
    }

    private static bool ProperlyIntersects(LSVector2 a, LSVector2 b, LSVector2 c, LSVector2 d) {
        double abC = Orientation(a, b, c), abD = Orientation(a, b, d);
        double cdA = Orientation(c, d, a), cdB = Orientation(c, d, b);
        return ((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon))
            && ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon));
    }

    private static bool SharesEndpoint((int From, int To) first, (int From, int To) second) {
        return first.From == second.From || first.From == second.To || first.To == second.From || first.To == second.To;
    }

    private static double Orientation(LSVector2 a, LSVector2 b, LSVector2 c) {
        return ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
    }

    private static double Cross(LSVector2 first, LSVector2 second) {
        return (double)first.X * second.Y - (double)first.Y * second.X;
    }

    private static double ProjectParameter(LSVector2 point, TriangulationConstraint segment) {
        var delta = segment.To - segment.From;
        double lengthSquared = (double)delta.X * delta.X + (double)delta.Y * delta.Y;
        return lengthSquared <= Epsilon ? 0d : ((point.X - segment.From.X) * delta.X + (point.Y - segment.From.Y) * delta.Y) / lengthSquared;
    }

    private static LSVector2 Interpolate(TriangulationConstraint segment, double parameter) {
        if (parameter <= Epsilon) return segment.From;
        if (parameter >= 1d - Epsilon) return segment.To;
        return new LSVector2(
            (float)(segment.From.X + (segment.To.X - segment.From.X) * parameter),
            (float)(segment.From.Y + (segment.To.Y - segment.From.Y) * parameter));
    }

    private static int GetOrAddVertex(LSVector2 point, List<LSVector2> vertices, Dictionary<QuantizedPoint, int> indices) {
        var key = QuantizedPoint.From(point);
        if (indices.TryGetValue(key, out int index)) return index;
        index = vertices.Count;
        vertices.Add(point);
        indices.Add(key, index);
        return index;
    }

    private static bool IsOnSegment(double parameter) => parameter >= -Epsilon && parameter <= 1d + Epsilon;
    private static double ClampParameter(double parameter) => Math.Clamp(parameter, 0d, 1d);
    private static (int From, int To) NormalizeEdge(int from, int to) => from < to ? (from, to) : (to, from);

    private sealed class ParameterComparer : IEqualityComparer<double> {
        public bool Equals(double first, double second) => Math.Abs(first - second) <= Epsilon;
        public int GetHashCode(double value) => 0;
    }

    private readonly record struct NodedConstraints(List<LSVector2> Vertices, List<(int From, int To)> Constraints);

    private readonly record struct QuantizedPoint(long X, long Y) {
        public static QuantizedPoint From(LSVector2 point) {
            return new QuantizedPoint((long)Math.Round(point.X * QuantizationScale), (long)Math.Round(point.Y * QuantizationScale));
        }
    }
}
