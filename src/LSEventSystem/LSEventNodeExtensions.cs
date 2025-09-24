namespace LSUtils.EventSystem;

public static class LSEventNodeExtensions {
    /// <summary>
    /// Checks if the node is currently in a WAITING state.
    /// </summary>
    /// <param name="node">The event node to check.</param>
    /// <returns>True if the node status is WAITING; otherwise, false.</returns>
    public static bool IsWaiting(this ILSEventNode node) {
        return node.GetNodeStatus() == LSEventProcessStatus.WAITING;
    }
    internal static LSEventCondition? UpdateConditions(bool overrideExisting, LSEventCondition? existingConditions, params LSEventCondition?[] conditions) {
        var defaultCondition = (LSEventCondition)((ctx, node) => true); // default condition that always returns true
        if (conditions == null || conditions.Length == 0) { //if no conditions are provided we set the default condition if overrideExisting is true
            if (overrideExisting) existingConditions = defaultCondition; // reset existing conditions
        } else {
            if (overrideExisting) existingConditions = defaultCondition; // reset existing conditions
            foreach (var condition in conditions) {
                if (condition != null) {
                    existingConditions += condition;
                }
            }
        }
        return existingConditions;
    }
}
