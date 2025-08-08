using System;
using LSUtils.EventSystem;

namespace LSUtils.EventSystem.Demo;

/// <summary>
/// Simple demonstration of the new clean phase-based event system.
/// </summary>
public class SimpleDemo {
    
    public static void RunDemo() {
        Console.WriteLine("=== LSUtils V2 Clean Event System Demo ===\n");
        
        // Create dispatcher and test target
        var dispatcher = new LSDispatcher();
        var player = new DemoPlayer("Alice", 15);
        
        // Register handlers in different phases
        RegisterValidationHandler(dispatcher);
        RegisterExecutionHandler(dispatcher);
        RegisterLoggingHandler(dispatcher);
        
        // Test successful event
        Console.WriteLine("1. Testing successful equipment...");
        var successEvent = new EquipItemEvent(player, "Steel Sword", 10);
        var result = dispatcher.ProcessEvent(successEvent);
        Console.WriteLine($"Result: {result}");
        Console.WriteLine($"Completed phases: {successEvent.CompletedPhases}");
        Console.WriteLine();
        
        // Test failing event
        Console.WriteLine("2. Testing failed equipment (item too powerful)...");
        var failEvent = new EquipItemEvent(player, "Legendary Weapon", 50);
        var failResult = dispatcher.ProcessEvent(failEvent);
        Console.WriteLine($"Result: {failResult}");
        Console.WriteLine($"Event aborted: {failEvent.IsCancelled}");
        Console.WriteLine($"Error: {failEvent.ErrorMessage}");
        Console.WriteLine($"Completed phases: {failEvent.CompletedPhases}");
        Console.WriteLine();
        
        Console.WriteLine("Demo completed!");
    }
    
    private static void RegisterValidationHandler(LSDispatcher dispatcher) {
        dispatcher.For<EquipItemEvent>()
            .InPhase(LSEventPhase.VALIDATE)
            .WithPriority(LSPhasePriority.HIGH)
            .Register((evt, ctx) => {
                Console.WriteLine($"  [VALIDATE] Checking if {evt.ItemName} can be equipped by {evt.Instance.Name}");
                
                // Simple validation: item level can't be more than double player level
                if (evt.ItemLevel > evt.Instance.Level * 2) {
                    evt.SetErrorMessage($"Item level {evt.ItemLevel} too high for player level {evt.Instance.Level}");
                    return LSPhaseResult.CANCEL;
                }
                
                evt.SetData("validation_time", DateTime.UtcNow);
                Console.WriteLine("  [VALIDATE] ✓ Validation passed");
                return LSPhaseResult.CONTINUE;
            });
    }
    
    private static void RegisterExecutionHandler(LSDispatcher dispatcher) {
        dispatcher.For<EquipItemEvent>()
            .InPhase(LSEventPhase.EXECUTE)
            .Register((evt, ctx) => {
                Console.WriteLine($"  [EXECUTE] Equipping {evt.ItemName} to {evt.Instance.Name}");
                
                // Simulate equipment logic
                evt.Instance.EquippedItem = evt.ItemName;
                evt.SetData("equipped_at", DateTime.UtcNow);
                
                Console.WriteLine("  [EXECUTE] ✓ Item equipped successfully");
                return LSPhaseResult.CONTINUE;
            });
    }
    
    private static void RegisterLoggingHandler(LSDispatcher dispatcher) {
        dispatcher.For<EquipItemEvent>()
            .InPhase(LSEventPhase.COMPLETE)
            .WithPriority(LSPhasePriority.BACKGROUND)
            .Register((evt, ctx) => {
                var success = evt.IsCompleted;
                var duration = ctx.ElapsedTime.TotalMilliseconds;
                
                Console.WriteLine($"  [COMPLETE] Equipment operation {(success ? "succeeded" : "failed")} in {duration:F1}ms");
                
                if (success && evt.TryGetData<DateTime>("validation_time", out var validationTime)) {
                    Console.WriteLine($"  [COMPLETE] Validation completed at {validationTime:HH:mm:ss.fff}");
                }
                
                return LSPhaseResult.CONTINUE;
            });
    }
}

/// <summary>
/// Simple player class for demo purposes.
/// </summary>
public class DemoPlayer {
    public string Name { get; }
    public int Level { get; }
    public string? EquippedItem { get; set; }
    
    public DemoPlayer(string name, int level) {
        Name = name;
        Level = level;
    }
    
    public override string ToString() => $"{Name} (Level {Level})";
}

/// <summary>
/// Simple event for item equipment.
/// </summary>
public class EquipItemEvent : LSEvent<DemoPlayer> {
    public string ItemName { get; }
    public int ItemLevel { get; }
    
    public EquipItemEvent(DemoPlayer player, string itemName, int itemLevel) 
        : base(player) {
        ItemName = itemName;
        ItemLevel = itemLevel;
    }
    
    public override string ToString() => $"EquipItemEvent: {ItemName} (Level {ItemLevel}) for {Instance}";
}
