namespace LSUtils.ProcessSystem;

public partial class LSProcessNodeParallel {
    /// <summary>
    /// Defines how to evaluate success and failure thresholds in parallel process nodes.
    /// </summary>
    public enum ParallelThresholdMode {
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
