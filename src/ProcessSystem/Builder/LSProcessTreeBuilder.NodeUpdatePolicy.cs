namespace LSUtils.ProcessSystem;

/// <summary>
/// Defines policies for updating existing nodes during process tree building.
/// </summary>
[System.Flags]
public enum NodeUpdatePolicy {
    /// <summary>
    /// No special update policy set.
    /// default behaviours:
    /// - existing layer nodes retain their type, children, and handlers
    /// - existing handlers retain their execution
    /// - existing nodes with different type builder action is skipped.
    /// </summary>
    NONE = 0,
    /// <summary>
    /// Mark the layer to ignore changes when the node is being updated.
    /// </summary>
    IGNORE_CHANGES = 1 << 1,
    /// <summary>
    /// Mark the layer to ignore builder actions when the node is being updated.
    /// </summary>
    IGNORE_BUILDER = 1 << 2,
    /// <summary>
    /// Replace existing nodes with the same ID.
    /// Existing children are lost (if any).
    /// Used for replace layers types (e.g.: sequence to selector), layers to handlers and vice versa.
    /// </summary>
    REPLACE_NODE = 1 << 3,
    /// <summary>
    /// Override existing handler implementations.
    /// Only handler nodes are affected by this flag.
    /// </summary>
    OVERRIDE_HANDLER = 1 << 4,
    /// <summary>
    /// Override existing node conditions.
    /// Ignore MERGE_CONDITIONS if both are set.
    /// </summary>
    OVERRIDE_CONDITIONS = 1 << 5,
    /// <summary>
    /// Merge new conditions with existing node conditions.
    /// Has no effect if OVERRIDE_CONDITIONS is also set.
    /// </summary>
    MERGE_CONDITIONS = 1 << 6,
    /// <summary>
    /// Override existing node priority.
    /// Keep existing priority if not set.
    /// </summary>
    OVERRIDE_PRIORITY = 1 << 7,
    /// <summary>
    /// Override existing parallel node success threshold.
    /// Used only by parallel layer nodes.
    /// </summary>
    OVERRIDE_PARALLEL_NUM_SUCCESS = 1 << 8,
    /// <summary>
    /// Override existing parallel node failure threshold.
    /// Used only by parallel layer nodes.
    /// </summary>
    OVERRIDE_PARALLEL_NUM_FAILURE = 1 << 9,
    /// <summary>
    /// Override existing parallel node threshold mode.
    /// Used only by parallel layer nodes.
    /// </summary>
    OVERRIDE_THRESHOLD_MODE = 1 << 10,
    /// <summary>
    /// Mark the node as read-only, preventing any modifications.
    /// This includes ignoring changes and builder actions.
    /// </summary>
    READONLY = IGNORE_CHANGES | IGNORE_BUILDER,
    /// <summary>
    /// Default policy for handler nodes.
    /// - override handler execution
    /// - keep existing priority
    /// - ignore conditions
    /// - does not replace existing layer nodes
    /// </summary>
    DEFAULT_HANDLER = OVERRIDE_HANDLER,
    /// <summary>
    /// Default policy for layer nodes.
    /// - keep priority
    /// - ignore new conditions
    /// - does not replace different (type) existing layer nodes.
    /// - skip builder actions when nodes are not the same type.
    /// </summary>
    DEFAULT_LAYER = NONE,
    /// <summary>
    /// Policy to fully protect a layer from modifications, including replacing layer, ignoring changes, overriding conditions, and overriding priority.
    /// When used in processing() override methods, it ensures the layer remains unchanged independently of other contexts.
    /// </summary>
    PROTECT_NODE = NodeUpdatePolicy.REPLACE_NODE | NodeUpdatePolicy.IGNORE_CHANGES | NodeUpdatePolicy.OVERRIDE_CONDITIONS | NodeUpdatePolicy.OVERRIDE_PRIORITY,
}
