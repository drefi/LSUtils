namespace LSUtils.ProcessSystem;

public partial class LSProcessNodeParallel {
    /// <summary>
    /// Defines how to evaluate success and failure thresholds in parallel process nodes.
    /// </summary>
    public enum ParallelThresholdMode {
        /// <summary>
        /// In this mode, the parallel node will return WAITING if any child node is still running, regardless of the success or failure thresholds.
        /// </summary>
        NONE,
        /// <summary>
        /// In case no threshold is set, reached or both thresholds are reached,
        /// success is prioritized over failure
        /// </summary>
        SUCCESS_PRIORITY,
        /// <summary>
        /// In case no threshold is set, reached or both thresholds are reached,
        /// failure is prioritized over success
        /// </summary>
        FAILURE_PRIORITY,
    }
}
