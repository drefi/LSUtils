using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentBehaviourTree {
    /// <summary>
    /// [DEPRECATED] Interface for behaviour tree nodes.
    /// Use LSProcessSystem (ILSProcessNode) instead.
    /// </summary>
    [System.Obsolete("Use LSProcessSystem instead. See MIGRATION_PLAN.md", false)]
    public interface IBehaviourTreeNode {
        /// <summary>
        /// Update the time of the behaviour tree.
        /// </summary>
        BehaviourTreeStatus Update(TimeData time);
    }
}
