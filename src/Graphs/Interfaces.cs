using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.Graphs {
    public interface IGraph<T> : IEnumerable<T> {
        int Count { get; }

        IEnumerable<T> GetNeighbours(T node);
        int GetNeighbours(T node, ICollection<T> buffer);
        bool Contains(T node);
    }
    /// <summary>
    /// Contract for a graph node that can determine its own neighbours.
    /// 
    /// When implementing this contract T should be typed as itself.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface INode<T> where T : INode<T> {

        IEnumerable<T> GetNeighbours();
        int GetNeighbours(ICollection<T> buffer);
    }
    public interface IHeuristic<T> {
        float Weight(T n);
        float Distance(T x, T y);
    }
    
    public interface IPathResolver<T> {
        T? Start { get; set; }
        T? Goal { get; set; }

        IList<T> Reduce();
        int Reduce(IList<T> path);
    }

    public interface ISteppingPathResolver<T> : IPathResolver<T> {
        /// <summary>
        /// Start the stepping path resolver for reducing.
        /// </summary>
        void BeginSteppedReduce();
        /// <summary>
        /// Take a step at reducing the path resolver.
        /// </summary>
        /// <returns>Returns true if reached goal.</returns>
        bool Step();
        /// <summary>
        /// Get the result of reducing the path.
        /// </summary>
        /// <param name="path"></param>
        int EndSteppedReduce(IList<T> path);
        /// <summary>
        /// Reset the resolver so a new Step sequence could be started.
        /// </summary>
        void Reset();
    }
}
