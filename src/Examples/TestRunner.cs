using LSUtils.Examples;

namespace LSUtils.TestRunner;

/// <summary>
/// Simple console application to test the phase-based event system migration.
/// </summary>
public class Program {
    public static void Main(string[] args) {
        Console.WriteLine("LSUtils Phase-Based Event System Test Runner");
        Console.WriteLine("===========================================\n");
        
        try {
            // Run the simple phase test first
            Console.WriteLine("Running Simple Phase Test...\n");
            SimplePhaseTest.RunTest();
            
            Console.WriteLine("\n" + new string('=', 50) + "\n");
            
            // Run the comprehensive dispatch test
            Console.WriteLine("Running Comprehensive Phase Dispatch Test...\n");
            PhaseDispatchTest.RunComprehensiveTest();
            
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("All tests completed successfully!");
            Console.WriteLine("The phase-based event system is working correctly.");
            
        } catch (Exception ex) {
            Console.WriteLine($"Test failed with error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
