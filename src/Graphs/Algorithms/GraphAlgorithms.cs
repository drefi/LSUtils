namespace LSUtils.Graphs.Algorithms;

using System;
using System.Collections.Generic;

public delegate float NodeDistanceFunc<TNode>(TNode from, TNode to);

public static class GraphAlgorithms {
    public static List<TNode> BreadthFirstSearch<TNode>(IGraph<TNode> graph, TNode start) where TNode : notnull {
        var result = new List<TNode>();
        if (!graph.HasNode(start)) return result;

        var visited = new HashSet<TNode>();
        var queue = new Queue<TNode>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0) {
            var node = queue.Dequeue();
            result.Add(node);

            foreach (var neighbor in graph.GetNeighbors(node)) {
                if (!visited.Add(neighbor)) continue;
                queue.Enqueue(neighbor);
            }
        }

        return result;
    }

    public static List<TNode> FloodFill<TNode>(
        IGraph<TNode> graph,
        TNode start,
        Predicate<TNode> canVisit) where TNode : notnull {
        var result = new List<TNode>();
        if (!graph.HasNode(start) || !canVisit(start)) return result;

        var visited = new HashSet<TNode>();
        var queue = new Queue<TNode>();

        visited.Add(start);
        queue.Enqueue(start);

        while (queue.Count > 0) {
            var node = queue.Dequeue();
            result.Add(node);

            foreach (var neighbor in graph.GetNeighbors(node)) {
                if (visited.Contains(neighbor) || !canVisit(neighbor)) continue;
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return result;
    }

    public static List<List<TNode>> ConnectedComponents<TNode>(IGraph<TNode> graph) where TNode : notnull {
        var components = new List<List<TNode>>();
        var visited = new HashSet<TNode>();

        foreach (var node in graph.Nodes) {
            if (visited.Contains(node)) continue;

            var component = new List<TNode>();
            var queue = new Queue<TNode>();

            visited.Add(node);
            queue.Enqueue(node);

            while (queue.Count > 0) {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var neighbor in graph.GetNeighbors(current)) {
                    if (!visited.Add(neighbor)) continue;
                    queue.Enqueue(neighbor);
                }
            }

            components.Add(component);
        }

        return components;
    }

    public static List<TNode> AStar<TNode>(
        IGraph<TNode> graph,
        TNode start,
        TNode goal,
        NodeDistanceFunc<TNode> heuristic,
        NodeDistanceFunc<TNode> cost) where TNode : notnull {
        if (!graph.HasNode(start) || !graph.HasNode(goal)) return new List<TNode>();

        var cameFrom = new Dictionary<TNode, TNode>();
        var gScore = new Dictionary<TNode, float> {
            [start] = 0f,
        };

        var openSet = new PriorityQueue<TNode, float>();
        openSet.Enqueue(start, heuristic(start, goal));

        while (openSet.Count > 0) {
            var current = openSet.Dequeue();

            if (EqualityComparer<TNode>.Default.Equals(current, goal)) {
                return ReconstructPath(cameFrom, current);
            }

            float currentScore = gScore[current];
            foreach (var neighbor in graph.GetNeighbors(current)) {
                float edgeCost = cost(current, neighbor);
                if (edgeCost < 0f) throw new LSArgumentException("A* does not support negative edge costs.", nameof(cost));

                float tentativeScore = currentScore + edgeCost;
                if (gScore.TryGetValue(neighbor, out float knownScore) && tentativeScore >= knownScore) continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeScore;
                openSet.Enqueue(neighbor, tentativeScore + heuristic(neighbor, goal));
            }
        }

        return new List<TNode>();
    }

    private static List<TNode> ReconstructPath<TNode>(Dictionary<TNode, TNode> cameFrom, TNode current) where TNode : notnull {
        var path = new List<TNode> { current };

        while (cameFrom.TryGetValue(current, out var previous)) {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
