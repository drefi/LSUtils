using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentBehaviourTree
{
    /// <summary>
    /// [DEPRECATED] The return type when invoking behaviour tree nodes.
    /// Use LSProcessResultStatus instead.
    /// </summary>
    [System.Obsolete("Use LSProcessResultStatus instead. See MIGRATION_PLAN.md", false)]
    public enum BehaviourTreeStatus
    {
        Success,
        Failure,
        Running
    }
}
