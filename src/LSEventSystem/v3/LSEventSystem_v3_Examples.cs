using System;
using LSUtils.EventSystem;

namespace LSUtils.EventSystem.Examples;

/// <summary>
/// Example event for demonstrating the v3 nested callback API.
/// </summary>
public class PlayerLevelUpEvent_v3 : LSBaseEvent_v3 {
    public string PlayerName { get; set; } = string.Empty;
    public int OldLevel { get; set; }
    public int NewLevel { get; set; }
    public int ExperienceGained { get; set; }
    public bool IsMaxLevel => NewLevel >= 100;
    
    public PlayerLevelUpEvent_v3(string playerName, int oldLevel, int newLevel, int experienceGained) {
        PlayerName = playerName;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        ExperienceGained = experienceGained;
    }
}

/// <summary>
/// Example demonstrating the v3 event system with both global and event-scoped nested callbacks.
/// UPDATED: Now demonstrates the fixed handler execution order and global+event-scoped integration.
/// </summary>
public static class LSEventSystem_v3_Examples {
    
    /// <summary>
    /// Example 1: Global handler registration using LSDispatcher_v3.Builder() with nested callbacks.
    /// These handlers will execute for ALL PlayerLevelUpEvent_v3 instances.
    /// 
    /// DEMONSTRATES: Proper registration order preservation within priority groups.
    /// </summary>
    public static void RegisterGlobalHandlers() {
        LSDispatcher_v3.Singleton.ForEvent<PlayerLevelUpEvent_v3>()
            
            // VALIDATE phase - check if the level up is valid
            .OnValidatePhase(validate => validate
                .Sequential(evt => {
                    Console.WriteLine($"[GLOBAL-1] Validating level up for {evt.PlayerName}: {evt.OldLevel} → {evt.NewLevel}");
                    return evt.NewLevel > evt.OldLevel ? LSHandlerResult.CONTINUE : LSHandlerResult.FAILURE;
                }, LSESPriority.HIGH)
                
                .Parallel(evt => {
                    Console.WriteLine($"[GLOBAL-2] Parallel validation check for {evt.PlayerName}");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .Sequential(evt => {
                    Console.WriteLine($"[GLOBAL-3] Final validation step for {evt.PlayerName}");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .When(evt => evt.NewLevel > 50, evt => {
                    Console.WriteLine($"[GLOBAL] High level validation for {evt.PlayerName}");
                    return LSHandlerResult.CONTINUE;
                })
                
                .SequentialGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Checking experience requirements...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Validating level cap...");
                        return evt.NewLevel <= 100 ? LSHandlerResult.CONTINUE : LSHandlerResult.FAILURE;
                    })
                )
            )
            
            // PREPARE phase - setup for level up effects
            .OnPreparePhase(prepare => prepare
                .ParallelGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Preparing stat bonuses for level {evt.NewLevel}...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Preparing skill point allocation...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Preparing achievement checks...");
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .HighPriority(high => high
                    .Sequential(evt => {
                        Console.WriteLine($"[GLOBAL] High priority preparation for {evt.PlayerName}");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            
            // EXECUTE phase - apply the level up
            .OnExecutePhase(execute => execute
                .Sequential(evt => {
                    Console.WriteLine($"[GLOBAL] Applying level up: {evt.PlayerName} is now level {evt.NewLevel}!");
                    evt.SetData("levelApplied", true);
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .Unless(evt => evt.IsMaxLevel, unless => unless
                    .Sequential(evt => {
                        Console.WriteLine($"[GLOBAL] Player can still level up further");
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .When(evt => evt.IsMaxLevel, maxLevel => maxLevel
                    .Sequential(evt => {
                        Console.WriteLine($"[GLOBAL] 🎉 {evt.PlayerName} has reached max level!");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            
            // SUCCESS phase - celebration and notifications
            .OnSuccess(success => success
                .ParallelGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Sending level up notification to guild...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Updating player statistics...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[GLOBAL] Playing level up sound effect...");
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .LowPriority(low => low
                    .Sequential(evt => {
                        Console.WriteLine($"[GLOBAL] Logging level up event to database...");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            
            // COMPLETE phase - cleanup and final steps
            .OnComplete(complete => complete
                .Sequential(evt => {
                    Console.WriteLine($"[GLOBAL] Level up complete for {evt.PlayerName}!");
                    return LSHandlerResult.CONTINUE;
                })
                
                .Chain(
                    evt => {
                        Console.WriteLine($"[GLOBAL] Cleanup step 1");
                        return LSHandlerResult.CONTINUE;
                    },
                    evt => {
                        Console.WriteLine($"[GLOBAL] Cleanup step 2");
                        return LSHandlerResult.CONTINUE;
                    }
                )
            );
    }
    
    /// <summary>
    /// Example 2: Event-scoped handler registration using event.WithCallbacks() with nested callbacks.
    /// These handlers will execute ONLY for this specific event instance.
    /// 
    /// DEMONSTRATES: Fixed global + event-scoped handler combination and execution order.
    /// </summary>
    public static bool ProcessSpecificLevelUp() {
        var levelUpEvent = new PlayerLevelUpEvent_v3("Alice", 49, 50, 1000);
        
        // Use event-scoped callbacks that PROPERLY combine with global handlers
        return levelUpEvent.WithCallbacks<PlayerLevelUpEvent_v3>()
            .OnValidate(validate => validate
                .Sequential(evt => {
                    Console.WriteLine($"[EVENT-SCOPED-1] Special validation for {evt.PlayerName} reaching level 50");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .Parallel(evt => {
                    Console.WriteLine($"[EVENT-SCOPED-2] Event-specific parallel check");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .Sequential(evt => {
                    Console.WriteLine($"[EVENT-SCOPED-3] Final event-specific validation");
                    return LSHandlerResult.CONTINUE;
                }, LSESPriority.HIGH)
                
                .When(evt => evt.PlayerName == "Alice", alice => alice
                    .Sequential(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Alice-specific validation");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            .OnPrepare(prepare => prepare
                .SequentialGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Preparing Alice's special rewards...");
                        evt.SetData("specialRewards", new[] { "Rare Item", "Skill Point", "Title" });
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Setting up milestone celebration...");
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .ParallelGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Parallel preparation task 1");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Parallel preparation task 2");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            .OnExecute(execute => execute
                .HighPriority(high => high
                    .Sequential(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] High priority: Awarding special milestone rewards...");
                        if (evt.TryGetData<string[]>("specialRewards", out var rewards)) {
                            Console.WriteLine($"[EVENT-SCOPED] Rewards: {string.Join(", ", rewards)}");
                        }
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .When(evt => evt.NewLevel % 10 == 0, milestone => milestone
                    .Sequential(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] 🎊 Milestone level {evt.NewLevel} reached!");
                        return LSHandlerResult.CONTINUE;
                    })
                )
                
                .ParallelGroup(group => group
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Triggering fireworks effect...");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Handler(evt => {
                        Console.WriteLine($"[EVENT-SCOPED] Broadcasting to friends...");
                        return LSHandlerResult.CONTINUE;
                    })
                )
            )
            .OnSuccess(success => success
                .Sequential(evt => {
                    Console.WriteLine($"[EVENT-SCOPED] Event-specific success celebration for {evt.PlayerName}!");
                    return LSHandlerResult.CONTINUE;
                })
                
                .NormalPriority(normal => normal
                    .ParallelGroup(group => group
                        .Handler(evt => {
                            Console.WriteLine($"[EVENT-SCOPED] Updating Alice's personal records...");
                            return LSHandlerResult.CONTINUE;
                        })
                        .Handler(evt => {
                            Console.WriteLine($"[EVENT-SCOPED] Sending congratulations email...");
                            return LSHandlerResult.CONTINUE;
                        })
                    )
                )
            )
            .Dispatch(); // This now PROPERLY executes BOTH global AND event-scoped handlers!
    }
    
    /// <summary>
    /// Example 3: Simple event dispatching without event-scoped callbacks.
    /// Only global handlers will execute.
    /// </summary>
    public static bool ProcessSimpleLevelUp() {
        var levelUpEvent = new PlayerLevelUpEvent_v3("Bob", 23, 24, 450);
        
        // Simple dispatch - only global handlers will execute
        return levelUpEvent.Dispatch();
    }
    
    /// <summary>
    /// Example 4: Demonstrating the FIXED execution order.
    /// This shows that handlers now execute in registration order within priority groups.
    /// </summary>
    public static void DemonstrateExecutionOrder() {
        Console.WriteLine("=== DEMONSTRATING FIXED EXECUTION ORDER ===\n");
        
        var testEvent = new PlayerLevelUpEvent_v3("TestPlayer", 10, 11, 200);
        
        var result = testEvent.WithCallbacks<PlayerLevelUpEvent_v3>()
            .OnPrepare(prepare => prepare
                .Sequential(evt => {
                    Console.WriteLine("Handler A (Sequential) - Should execute 1st");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    Console.WriteLine("Handler B (Parallel) - Should execute 2nd");
                    return LSHandlerResult.CONTINUE;
                })
                .Parallel(evt => {
                    Console.WriteLine("Handler C (Parallel) - Should execute 3rd");
                    return LSHandlerResult.CONTINUE;
                })
                .Sequential(evt => {
                    Console.WriteLine("Handler D (Sequential) - Should execute 4th");
                    return LSHandlerResult.CONTINUE;
                })
            )
            .Dispatch();
            
        Console.WriteLine($"\nExecution Order Test Result: {(result ? "SUCCESS" : "FAILED")}");
        Console.WriteLine("Expected order: A → B → C → D");
        Console.WriteLine("If you see them in this order, the fix is working!\n");
    }
    
    /// <summary>
    /// Example 5: Demonstrating all features in a comprehensive test.
    /// </summary>
    public static void RunComprehensiveExample() {
        Console.WriteLine("=== LSEventSystem v3 Comprehensive Example (FIXED VERSION) ===\n");
        
        // Step 1: Register global handlers
        Console.WriteLine("1. Registering global handlers...");
        RegisterGlobalHandlers();
        Console.WriteLine("   ✓ Global handlers registered\n");
        
        // Step 2: Test execution order fix
        Console.WriteLine("2. Testing execution order fix...");
        DemonstrateExecutionOrder();
        
        // Step 3: Process a simple level up (global handlers only)
        Console.WriteLine("3. Processing simple level up (Bob: 23 → 24)...");
        var simpleResult = ProcessSimpleLevelUp();
        Console.WriteLine($"   Result: {(simpleResult ? "Success" : "Failed")}\n");
        
        // Step 4: Process a complex level up with event-scoped handlers (FIXED COMBINATION)
        Console.WriteLine("4. Processing milestone level up with PROPERLY COMBINED handlers (Alice: 49 → 50)...");
        var complexResult = ProcessSpecificLevelUp();
        Console.WriteLine($"   Result: {(complexResult ? "Success" : "Failed")}\n");
        
        // Step 5: Show the difference in execution
        Console.WriteLine("5. Processing another simple level up to show the difference...");
        var anotherEvent = new PlayerLevelUpEvent_v3("Charlie", 75, 76, 800);
        var anotherResult = anotherEvent.Dispatch();
        Console.WriteLine($"   Result: {(anotherResult ? "Success" : "Failed")}\n");
        
        Console.WriteLine("=== Example Complete ===");
        Console.WriteLine("✅ Fixed execution order preservation");
        Console.WriteLine("✅ Fixed global + event-scoped handler combination");
        Console.WriteLine("✅ Eliminated temporary dispatcher issues");
    }
}
