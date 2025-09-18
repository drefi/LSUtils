namespace LSUtils.EventSystem;

public delegate bool LSEventCondition(LSEvent @event, ILSEventNode node);

public static class LSEventConditions { 
    public static bool IsMet(LSEvent @event, ILSEventNode node) {
        foreach (LSEventCondition c in node.Conditions.GetInvocationList()) {
            if (!c(@event, node)) return false;
        }
        return true;
    }
}
