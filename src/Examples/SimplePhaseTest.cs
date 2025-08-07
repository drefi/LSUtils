using LSUtils;

namespace LSUtils.Examples;

/// <summary>
/// Simple test to verify the phase-based event system is working.
/// </summary>
public static class SimplePhaseTest {
    public class TestEvent : LSEvent {
        public List<string> ExecutionLog { get; } = new List<string>();
        
        public TestEvent() : base(LSEventOptions.Create()) { }
    }
    
    public static void RunTest() {
        Console.WriteLine("=== Phase-based Event System Test ===");
        
        var dispatcher = LSDispatcher.Instance;
        var testEvent = new TestEvent();
        
        // Register listeners in different phases
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("DISPATCH Phase");
            Console.WriteLine("✓ DISPATCH Phase executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.DISPATCH);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("PRE_EXECUTION Phase - HIGH Priority");
            Console.WriteLine("✓ PRE_EXECUTION Phase (HIGH) executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.PRE_EXECUTION, PhasePriority.HIGH);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("PRE_EXECUTION Phase - NORMAL Priority");
            Console.WriteLine("✓ PRE_EXECUTION Phase (NORMAL) executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.PRE_EXECUTION, PhasePriority.NORMAL);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("EXECUTION Phase");
            Console.WriteLine("✓ EXECUTION Phase executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.EXECUTION);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("POST_EXECUTION Phase");
            Console.WriteLine("✓ POST_EXECUTION Phase executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.POST_EXECUTION);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("SUCCESS Phase");
            Console.WriteLine("✓ SUCCESS Phase executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.SUCCESS);
        
        dispatcher.RegisterStatus<TestEvent>((id, evt) => {
            evt.ExecutionLog.Add("COMPLETE Phase");
            Console.WriteLine("✓ COMPLETE Phase executed");
            return EventProcessingStatus.SUCCESS;
        }, EventPhase.COMPLETE);
        
        // Dispatch the event
        Console.WriteLine("\nDispatching event...");
        bool success = testEvent.Dispatch();
        
        Console.WriteLine($"\nEvent completed successfully: {success}");
        Console.WriteLine("Execution order:");
        
        for (int i = 0; i < testEvent.ExecutionLog.Count; i++) {
            Console.WriteLine($"{i + 1}. {testEvent.ExecutionLog[i]}");
        }
        
        // Verify correct order
        var expectedOrder = new[] {
            "DISPATCH Phase",
            "PRE_EXECUTION Phase - HIGH Priority",
            "PRE_EXECUTION Phase - NORMAL Priority", 
            "EXECUTION Phase",
            "POST_EXECUTION Phase",
            "SUCCESS Phase",
            "COMPLETE Phase"
        };
        
        bool orderCorrect = testEvent.ExecutionLog.SequenceEqual(expectedOrder);
        Console.WriteLine($"\nExecution order correct: {orderCorrect}");
        
        if (!orderCorrect) {
            Console.WriteLine("Expected:");
            for (int i = 0; i < expectedOrder.Length; i++) {
                Console.WriteLine($"{i + 1}. {expectedOrder[i]}");
            }
        }
    }
}
