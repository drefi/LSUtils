using LSUtils;

namespace LSUtils.Examples;

/// <summary>
/// Example demonstrating the phase-based event system migration.
/// This shows both old and new approaches side by side.
/// </summary>
public static class PhaseBasedEventExample {
    
    // Example event for demonstration
    public class DataProcessingEvent : LSEvent {
        public string Data { get; }
        public bool IsValid { get; set; }
        public string ProcessedResult { get; set; } = string.Empty;
        
        public DataProcessingEvent(string data) : base(LSEventOptions.Create()) {
            Data = data;
        }
    }
    
    public static void RunExample() {
        var dispatcher = LSDispatcher.Instance;
        
        // =============================================================
        // OLD APPROACH - Manual signaling in listeners
        // =============================================================
        Console.WriteLine("=== OLD APPROACH ===");
        
        // Old style listener - must manually call Signal/Failure/Cancel
        var oldListenerId = dispatcher.Register<DataProcessingEvent>((id, evt) => {
            Console.WriteLine($"[OLD] Processing: {evt.Data}");
            
            // Manual validation
            if (string.IsNullOrEmpty(evt.Data)) {
                Console.WriteLine("[OLD] Validation failed - data is empty");
                evt.Failure("Data is empty");
                return;
            }
            
            // Manual processing
            evt.ProcessedResult = evt.Data.ToUpper();
            Console.WriteLine($"[OLD] Processed result: {evt.ProcessedResult}");
            
            // Must remember to signal completion
            evt.Signal();
        });
        
        // =============================================================
        // NEW APPROACH - Phase-based with status returns
        // =============================================================
        Console.WriteLine("\n=== NEW APPROACH ===");
        
        // Validation phase - runs first
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[VALIDATION] Checking data: {evt.Data}");
                
                if (string.IsNullOrEmpty(evt.Data)) {
                    Console.WriteLine("[VALIDATION] Data is empty - FAILURE");
                    return EventProcessingStatus.FAILURE;
                }
                
                if (evt.Data.Length > 100) {
                    Console.WriteLine("[VALIDATION] Data too long - CANCEL");
                    return EventProcessingStatus.CANCEL;
                }
                
                evt.IsValid = true;
                Console.WriteLine("[VALIDATION] Data is valid - SUCCESS");
                return EventProcessingStatus.SUCCESS;
            },
            phase: EventPhase.PRE_EXECUTION,
            priority: PhasePriority.CRITICAL
        );
        
        // Main processing phase - runs after validation
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[EXECUTION] Processing: {evt.Data}");
                
                if (!evt.IsValid) {
                    Console.WriteLine("[EXECUTION] Skipping - data not validated");
                    return EventProcessingStatus.SKIP;
                }
                
                // Simulate processing
                evt.ProcessedResult = evt.Data.ToUpper().Trim();
                Console.WriteLine($"[EXECUTION] Result: {evt.ProcessedResult}");
                return EventProcessingStatus.SUCCESS;
            },
            phase: EventPhase.EXECUTION,
            priority: PhasePriority.NORMAL
        );
        
        // Success notification phase - only runs if everything succeeded
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[SUCCESS] Processing completed successfully!");
                Console.WriteLine($"[SUCCESS] Final result: {evt.ProcessedResult}");
                return EventProcessingStatus.SUCCESS;
            },
            phase: EventPhase.SUCCESS,
            priority: PhasePriority.LOW
        );
        
        // Failure handling phase - only runs if something failed
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[FAILURE] Processing failed, performing cleanup...");
                evt.ProcessedResult = "FAILED";
                return EventProcessingStatus.SUCCESS;
            },
            phase: EventPhase.FAILURE,
            priority: PhasePriority.HIGH
        );
        
        // Always runs - final cleanup
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[COMPLETE] Final cleanup, result: {evt.ProcessedResult}");
                return EventProcessingStatus.SUCCESS;
            },
            phase: EventPhase.COMPLETE,
            priority: PhasePriority.NORMAL
        );
        
        // =============================================================
        // TEST THE EXAMPLES
        // =============================================================
        Console.WriteLine("\n=== TESTING ===");
        
        // Test 1: Valid data
        Console.WriteLine("\n--- Test 1: Valid Data ---");
        var event1 = new DataProcessingEvent("hello world");
        event1.Dispatch();
        
        // Test 2: Empty data (should fail)
        Console.WriteLine("\n--- Test 2: Empty Data ---");
        var event2 = new DataProcessingEvent("");
        event2.Dispatch();
        
        // Test 3: Too long data (should cancel)
        Console.WriteLine("\n--- Test 3: Too Long Data ---");
        var event3 = new DataProcessingEvent(new string('x', 150));
        event3.Dispatch();
    }
    
    /// <summary>
    /// Example showing async processing with RUNNING status.
    /// </summary>
    public static async Task RunAsyncExample() {
        var dispatcher = LSDispatcher.Instance;
        
        Console.WriteLine("\n=== ASYNC PROCESSING EXAMPLE ===");
        
        // Async listener that returns RUNNING
        dispatcher.RegisterStatus<DataProcessingEvent>(
            (id, evt) => {
                Console.WriteLine($"[ASYNC] Starting async processing: {evt.Data}");
                
                // Start async operation
                _ = Task.Run(async () => {
                    await Task.Delay(2000); // Simulate work
                    
                    evt.ProcessedResult = $"ASYNC_{evt.Data.ToUpper()}";
                    Console.WriteLine($"[ASYNC] Async work completed: {evt.ProcessedResult}");
                    
                    // Signal completion
                    evt.Signal();
                });
                
                return EventProcessingStatus.RUNNING; // Still processing
            },
            phase: EventPhase.EXECUTION
        );
        
        var asyncEvent = new DataProcessingEvent("async test");
        asyncEvent.Dispatch();
        
        // Wait for completion
        while (!asyncEvent.IsDone && !asyncEvent.HasFailed && !asyncEvent.IsCancelled) {
            await Task.Delay(100);
        }
        
        Console.WriteLine($"[ASYNC] Final result: {asyncEvent.ProcessedResult}");
    }
}
