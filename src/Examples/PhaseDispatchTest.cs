using LSUtils;

namespace LSUtils.Examples;

/// <summary>
/// Comprehensive test to verify the phase-based dispatch system.
/// Tests execution order, status handling, and flow control.
/// </summary>
public static class PhaseDispatchTest {
    
    public class TestEvent : LSEvent {
        public List<string> ExecutionLog { get; } = new List<string>();
        public string TestData { get; set; } = "";
        public bool ShouldFail { get; set; } = false;
        public bool ShouldCancel { get; set; } = false;
        
        public TestEvent(string testData = "test") : base(LSEventOptions.Create()) {
            TestData = testData;
        }
    }
    
    public static void RunComprehensiveTest() {
        Console.WriteLine("=== Phase-Based Dispatch System Test ===\n");
        
        var dispatcher = LSDispatcher.Instance;
        
        // Clear any existing listeners for clean test
        ClearExistingListeners();
        
        // Test 1: Normal flow (success path)
        Console.WriteLine("--- Test 1: Normal Success Flow ---");
        RunSuccessFlowTest(dispatcher);
        
        Console.WriteLine("\n--- Test 2: Failure Flow ---");
        RunFailureFlowTest(dispatcher);
        
        Console.WriteLine("\n--- Test 3: Cancel Flow ---");
        RunCancelFlowTest(dispatcher);
        
        Console.WriteLine("\n--- Test 4: Priority Ordering ---");
        RunPriorityTest(dispatcher);
        
        Console.WriteLine("\n--- Test 5: Status-Based Listeners ---");
        RunStatusListenerTest(dispatcher);
    }
    
    private static void ClearExistingListeners() {
        // This would ideally clear listeners, but for now we'll work with existing ones
        // In a real scenario, you'd want a way to clear test listeners
    }
    
    private static void RunSuccessFlowTest(LSDispatcher dispatcher) {
        var testEvent = new TestEvent("success_test");
        
        // Register listeners for each phase
        RegisterPhaseListeners(dispatcher, "Success");
        
        // Dispatch the event
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"Event completed successfully: {success}");
        Console.WriteLine("Execution order:");
        for (int i = 0; i < testEvent.ExecutionLog.Count; i++) {
            Console.WriteLine($"{i + 1}. {testEvent.ExecutionLog[i]}");
        }
        
        // Verify expected phases were executed
        var expectedPhases = new[] { "DISPATCH", "PRE_EXECUTION", "EXECUTION", "POST_EXECUTION", "SUCCESS", "COMPLETE" };
        bool hasAllPhases = expectedPhases.All(phase => testEvent.ExecutionLog.Any(log => log.Contains(phase)));
        Console.WriteLine($"All expected phases executed: {hasAllPhases}");
    }
    
    private static void RunFailureFlowTest(LSDispatcher dispatcher) {
        var testEvent = new TestEvent("failure_test") { ShouldFail = true };
        
        // Register listeners for each phase including failure handling
        RegisterPhaseListeners(dispatcher, "Failure");
        
        // Add a listener that will fail during execution
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("EXECUTION - Failure Trigger");
            Console.WriteLine("✓ EXECUTION Phase - Triggering failure");
            
            if (evt.ShouldFail) {
                return EventProcessingStatus.FAILURE;
            }
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION, PhasePriority.CRITICAL);
        
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"Event completed (expected failure): {success}");
        Console.WriteLine("Execution order:");
        for (int i = 0; i < testEvent.ExecutionLog.Count; i++) {
            Console.WriteLine($"{i + 1}. {testEvent.ExecutionLog[i]}");
        }
        
        // Verify failure phase was executed
        bool hasFailurePhase = testEvent.ExecutionLog.Any(log => log.Contains("FAILURE"));
        Console.WriteLine($"Failure phase executed: {hasFailurePhase}");
    }
    
    private static void RunCancelFlowTest(LSDispatcher dispatcher) {
        var testEvent = new TestEvent("cancel_test") { ShouldCancel = true };
        
        RegisterPhaseListeners(dispatcher, "Cancel");
        
        // Add a listener that will cancel during pre-execution
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("PRE_EXECUTION - Cancel Trigger");
            Console.WriteLine("✓ PRE_EXECUTION Phase - Triggering cancel");
            
            if (evt.ShouldCancel) {
                return EventProcessingStatus.CANCEL;
            }
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.PRE_EXECUTION, PhasePriority.CRITICAL);
        
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"Event completed (expected cancel): {success}");
        Console.WriteLine("Execution order:");
        for (int i = 0; i < testEvent.ExecutionLog.Count; i++) {
            Console.WriteLine($"{i + 1}. {testEvent.ExecutionLog[i]}");
        }
        
        bool hasCancelPhase = testEvent.ExecutionLog.Any(log => log.Contains("CANCEL"));
        Console.WriteLine($"Cancel phase executed: {hasCancelPhase}");
    }
    
    private static void RunPriorityTest(LSDispatcher dispatcher) {
        var testEvent = new TestEvent("priority_test");
        
        // Register multiple listeners in same phase with different priorities
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("EXECUTION - MINIMAL Priority (should be last)");
            Console.WriteLine("✓ EXECUTION Phase - MINIMAL Priority");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION, PhasePriority.MINIMAL);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("EXECUTION - CRITICAL Priority (should be first)");
            Console.WriteLine("✓ EXECUTION Phase - CRITICAL Priority");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION, PhasePriority.CRITICAL);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("EXECUTION - NORMAL Priority (should be middle)");
            Console.WriteLine("✓ EXECUTION Phase - NORMAL Priority");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION, PhasePriority.NORMAL);
        
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"Event completed: {success}");
        Console.WriteLine("Priority execution order:");
        
        var executionEntries = testEvent.ExecutionLog.Where(log => log.Contains("EXECUTION")).ToList();
        for (int i = 0; i < executionEntries.Count; i++) {
            Console.WriteLine($"{i + 1}. {executionEntries[i]}");
        }
        
        // Verify priority order (CRITICAL should come before NORMAL, NORMAL before MINIMAL)
        int criticalIndex = executionEntries.FindIndex(log => log.Contains("CRITICAL"));
        int normalIndex = executionEntries.FindIndex(log => log.Contains("NORMAL"));
        int minimalIndex = executionEntries.FindIndex(log => log.Contains("MINIMAL"));
        
        bool priorityOrderCorrect = criticalIndex < normalIndex && normalIndex < minimalIndex;
        Console.WriteLine($"Priority order correct: {priorityOrderCorrect}");
    }
    
    private static void RunStatusListenerTest(LSDispatcher dispatcher) {
        var testEvent = new TestEvent("status_test");
        
        // Test different status returns
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("Status: SUCCESS");
            Console.WriteLine("✓ Status listener returning SUCCESS");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION, PhasePriority.HIGH);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("Status: SKIP");
            Console.WriteLine("✓ Status listener returning SKIP");
            return EventProcessingStatus.SKIP;
        }, EventPhase.EXECUTION, PhasePriority.NORMAL);
        
        // Test status listener with message
        LSStatusListenerWithMessage<TestEvent> messageListener = (System.Guid id, TestEvent evt, out string? message) => {
            message = "Processing completed with detailed info";
            evt.ExecutionLog.Add($"Status with message: {message}");
            Console.WriteLine($"✓ Status listener with message: {message}");
            return EventProcessingStatus.SUCCESS;
        };
        
        dispatcher.RegisterStatus<TestEvent>(messageListener, EventPhase.EXECUTION, PhasePriority.LOW);
        
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"Event completed: {success}");
        Console.WriteLine("Status listener results:");
        var statusEntries = testEvent.ExecutionLog.Where(log => log.Contains("Status")).ToList();
        for (int i = 0; i < statusEntries.Count; i++) {
            Console.WriteLine($"{i + 1}. {statusEntries[i]}");
        }
    }
    
    private static void RegisterPhaseListeners(LSDispatcher dispatcher, string testName) {
        // Register a listener for each phase to track execution order
        var phases = new[] {
            EventPhase.DISPATCH,
            EventPhase.PRE_EXECUTION, 
            EventPhase.EXECUTION,
            EventPhase.POST_EXECUTION,
            EventPhase.SUCCESS,
            EventPhase.FAILURE,
            EventPhase.CANCEL,
            EventPhase.COMPLETE
        };
        
        foreach (var phase in phases) {
            dispatcher.RegisterStatus<TestEvent>((id, evt) => {
                string logEntry = $"{testName} - {phase} Phase";
                evt.ExecutionLog.Add(logEntry);
                Console.WriteLine($"✓ {logEntry}");
                return EventProcessingStatus.SUCCESS;
            }, phase, PhasePriority.NORMAL);
        }
    }
}
