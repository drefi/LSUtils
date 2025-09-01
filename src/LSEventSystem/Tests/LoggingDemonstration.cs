using NUnit.Framework;
using System;
using System.Collections.Generic;
using LSUtils.EventSystem;
using LSUtils.EventSystem.Logging;

namespace LSUtils.Tests {
    [TestFixture]
    public class LoggingDemonstration {
        
        // Test events for demonstration
        private class DemoUserRegistrationEvent : LSEvent<DemoUser> {
            public string RegistrationSource { get; }
            public DateTime RegistrationTime { get; }
            public bool RequiresEmailVerification { get; }

            public DemoUserRegistrationEvent(DemoUser user, string source, bool requiresVerification = true)
                : base(user) {
                RegistrationSource = source;
                RegistrationTime = DateTime.UtcNow;
                RequiresEmailVerification = requiresVerification;
                SetData("registration.source", source);
                SetData("verification.required", requiresVerification);
            }
        }

        private class DemoSystemStartupEvent : LSBaseEvent {
            public string Version { get; }
            public DateTime StartupTime { get; }

            public DemoSystemStartupEvent(string version) {
                Version = version;
                StartupTime = DateTime.UtcNow;
            }
        }

        private class DemoUser {
            public string Email { get; set; } = "";
            public string Name { get; set; } = "";
            public int Age { get; set; }
            public bool IsActive { get; set; } = true;
        }

        [SetUp]
        public void Setup() {
            // Reset logging system for each test
            LSEventLogger.Instance.ClearProviders();
            LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
            LSEventLogger.Instance.MinimumLevel = LSLogLevel.DEBUG;
        }

        [Test]
        public void Demo_All_Log_Levels_With_Console_Provider() {
            Console.WriteLine("\n=== DEMONSTRATION: All Log Levels with Console Provider ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "demo@example.com", Name = "Demo User", Age = 25 };

            // Configure logging to show all levels
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.DEBUG);
            
            Console.WriteLine("📋 Processing event with DEBUG level logging enabled...\n");

            var userEvent = new DemoUserRegistrationEvent(user, "web-demo");
            userEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    Console.WriteLine("🔍 User-defined validation logic executing...");
                    // This will generate DEBUG level logs from the dispatcher
                    return LSHandlerResult.CONTINUE;
                })
                .OnPreparePhase((evt, ctx) => {
                    Console.WriteLine("⚙️ User-defined preparation logic executing...");
                    // This will generate DEBUG level logs from the dispatcher
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    Console.WriteLine("🚀 User-defined execution logic executing...");
                    // This will generate DEBUG level logs from the dispatcher
                    evt.SetData("execution.completed", true);
                    return LSHandlerResult.CONTINUE;
                })
                .OnSuccess(evt => {
                    Console.WriteLine("✅ User-defined success callback executing...");
                })
                .Dispatch();

            Console.WriteLine($"\n📊 Event completed successfully: {userEvent.IsCompleted}");
            Console.WriteLine("📋 Above you can see DEBUG level logs showing handler execution details\n");
        }

        [Test]
        public void Demo_Info_Level_Filtering() {
            Console.WriteLine("\n=== DEMONSTRATION: INFO Level Filtering ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "info-demo@example.com", Name = "Info Demo User", Age = 30 };

            // Configure logging to show INFO and above (filters out DEBUG)
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.INFO);
            
            Console.WriteLine("📋 Processing event with INFO level filtering (DEBUG messages hidden)...\n");

            var userEvent = new DemoUserRegistrationEvent(user, "mobile-demo");
            userEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    // Manually generate an INFO level log
                    LSEventLogger.Instance.Info("Custom INFO: Validation phase processing important business rule");
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    // Manually generate an INFO level log
                    LSEventLogger.Instance.Info("Custom INFO: Executing critical business operation");
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            Console.WriteLine("\n📊 Notice: DEBUG logs are filtered out, only INFO and above are shown\n");
        }

        [Test]
        public void Demo_Warning_And_Error_Scenarios() {
            Console.WriteLine("\n=== DEMONSTRATION: WARNING and ERROR Level Scenarios ===\n");
            
            var dispatcher = new LSDispatcher();
            
            // Configure logging to show WARNING and above
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.WARNING);
            
            Console.WriteLine("📋 Processing events with WARNING/ERROR scenarios...\n");

            // Scenario 1: Warning for cancellation
            var invalidUser = new DemoUser { Email = "", Name = "Invalid User", Age = 25 };
            var cancelEvent = new DemoUserRegistrationEvent(invalidUser, "warning-demo");
            
            Console.WriteLine("🟡 Scenario 1: Event cancellation (generates WARNING)");
            cancelEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    if (string.IsNullOrEmpty(evt.Instance.Email)) {
                        evt.SetErrorMessage("Email validation failed");
                        return LSHandlerResult.CANCEL; // This generates WARNING level log
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            // Scenario 2: Error for failure
            var failureUser = new DemoUser { Email = "failure@example.com", Name = "Failure User", Age = 15 };
            var failureEvent = new DemoUserRegistrationEvent(failureUser, "error-demo");
            
            Console.WriteLine("\n🔴 Scenario 2: Event failure (generates ERROR)");
            failureEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    if (evt.Instance.Age < 18) {
                        // Manually set failure and generate ERROR log
                        LSEventLogger.Instance.Error("Custom ERROR: Age validation failed - user too young");
                        if (evt is LSBaseEvent baseEvent) {
                            baseEvent.HasFailures = true;
                        }
                    }
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            Console.WriteLine("\n📊 Notice: Only WARNING and ERROR messages are shown\n");
        }

        [Test]
        public void Demo_Critical_Exception_Handling() {
            Console.WriteLine("\n=== DEMONSTRATION: CRITICAL Level Exception Handling ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "critical@example.com", Name = "Critical User", Age = 25 };

            // Configure logging to show all levels to see the progression
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.DEBUG);
            
            Console.WriteLine("📋 Processing event with intentional exception (generates CRITICAL log)...\n");

            var criticalEvent = new DemoUserRegistrationEvent(user, "critical-demo");
            
            try {
                criticalEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                    .OnExecutePhase((evt, ctx) => {
                        Console.WriteLine("💥 User handler about to throw exception...");
                        throw new InvalidOperationException("Demo exception for CRITICAL logging");
                    })
                    .Dispatch();
            } catch (InvalidOperationException ex) {
                Console.WriteLine($"\n⚠️ Exception caught by test: {ex.Message}");
                Console.WriteLine("📊 Notice: Exception was logged at CRITICAL level before propagating\n");
            }
        }

        [Test]
        public void Demo_Structured_Logging_With_Properties() {
            Console.WriteLine("\n=== DEMONSTRATION: Structured Logging with Properties ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "structured@example.com", Name = "Structured User", Age = 28 };

            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.DEBUG);
            
            Console.WriteLine("📋 Processing event with custom structured logging...\n");

            var structuredEvent = new DemoUserRegistrationEvent(user, "structured-demo");
            structuredEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    // Custom structured logging with properties
                    var properties = new Dictionary<string, object> {
                        ["user_email"] = evt.Instance.Email,
                        ["user_age"] = evt.Instance.Age,
                        ["registration_source"] = evt.RegistrationSource,
                        ["validation_time_ms"] = ctx.ElapsedTime.TotalMilliseconds
                    };
                    
                    LSEventLogger.Instance.Info("Custom structured log with user data", 
                        "UserRegistration", evt.ID, properties);
                    
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    // Another structured log with different context
                    var properties = new Dictionary<string, object> {
                        ["operation"] = "user_creation",
                        ["source"] = evt.RegistrationSource,
                        ["requires_verification"] = evt.RequiresEmailVerification
                    };
                    
                    LSEventLogger.Instance.Info("Business operation executing with context", 
                        "BusinessLogic", evt.ID, properties);
                    
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            Console.WriteLine("\n📊 Notice: Structured logs include properties for better context\n");
        }

        [Test]
        public void Demo_Multiple_Providers_Godot_Simulation() {
            Console.WriteLine("\n=== DEMONSTRATION: Multiple Providers (Godot + Console) ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "multi@example.com", Name = "Multi Provider User", Age = 32 };

            // Clear existing providers and add multiple
            LSEventLogger.Instance.ClearProviders();
            LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
            LSEventLogger.Instance.AddProvider(new LSGodotLogProvider()); // Will fallback to reflection
            
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.INFO);
            
            Console.WriteLine("📋 Processing event with multiple providers (Console + Godot simulation)...\n");
            Console.WriteLine("🎮 Note: Godot provider will attempt reflection-based logging\n");

            var multiEvent = new DemoUserRegistrationEvent(user, "multi-provider-demo");
            multiEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    LSEventLogger.Instance.Info("Multi-provider logging demonstration");
                    LSEventLogger.Instance.Warning("This message goes to both Console and Godot providers");
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            Console.WriteLine("\n📊 Messages sent to both Console and Godot log providers\n");
        }

        [Test]
        public void Demo_Silent_Logging_With_Null_Provider() {
            Console.WriteLine("\n=== DEMONSTRATION: Silent Logging (Production Mode) ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "silent@example.com", Name = "Silent User", Age = 40 };

            // Use null provider for silent operation
            LSEventLogger.Instance.ClearProviders();
            LSEventLogger.Instance.AddProvider(new LSNullLogProvider());
            
            Console.WriteLine("📋 Processing event with null provider (silent mode)...\n");
            Console.WriteLine("🔇 All logging output will be suppressed...\n");

            var silentEvent = new DemoUserRegistrationEvent(user, "silent-demo");
            silentEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnValidatePhase((evt, ctx) => {
                    LSEventLogger.Instance.Debug("This debug message is silenced");
                    LSEventLogger.Instance.Info("This info message is silenced");
                    LSEventLogger.Instance.Warning("This warning message is silenced");
                    LSEventLogger.Instance.Error("This error message is silenced");
                    return LSHandlerResult.CONTINUE;
                })
                .OnExecutePhase((evt, ctx) => {
                    LSEventLogger.Instance.Critical("Even critical messages are silenced");
                    return LSHandlerResult.CONTINUE;
                })
                .Dispatch();

            Console.WriteLine("📊 Event completed successfully with no log output (silent mode)\n");
            Console.WriteLine("✅ This demonstrates production mode where logging is disabled\n");
        }

        [Test]
        public void Demo_Log_Level_Progression_Same_Event() {
            Console.WriteLine("\n=== DEMONSTRATION: Log Level Progression on Same Event ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "progression@example.com", Name = "Progression User", Age = 35 };

            Console.WriteLine("📋 Demonstrating how different log levels filter the same event...\n");

            // Test with each log level
            var logLevels = new[] { 
                LSLogLevel.DEBUG, 
                LSLogLevel.INFO, 
                LSLogLevel.WARNING, 
                LSLogLevel.ERROR, 
                LSLogLevel.CRITICAL 
            };

            foreach (var level in logLevels) {
                Console.WriteLine($"\n--- Setting minimum level to: {level} ---");
                LSEventLogger.Instance.MinimumLevel =(level);
                
                var progressionEvent = new DemoUserRegistrationEvent(user, $"progression-{level.ToString().ToLower()}");
                progressionEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                    .OnValidatePhase((evt, ctx) => {
                        LSEventLogger.Instance.Debug($"DEBUG: Validation for {level} level test");
                        LSEventLogger.Instance.Info($"INFO: Processing for {level} level test");
                        LSEventLogger.Instance.Warning($"WARNING: Potential issue for {level} level test");
                        LSEventLogger.Instance.Error($"ERROR: Simulated error for {level} level test");
                        LSEventLogger.Instance.Critical($"CRITICAL: Simulated critical issue for {level} level test");
                        return LSHandlerResult.CONTINUE;
                    })
                    .Dispatch();
            }

            Console.WriteLine("\n📊 Notice how each level filters out messages below its threshold\n");
        }

        [Test]
        public void Demo_Async_Operations_With_Logging() {
            Console.WriteLine("\n=== DEMONSTRATION: Async Operations with Comprehensive Logging ===\n");
            
            var dispatcher = new LSDispatcher();
            var user = new DemoUser { Email = "async@example.com", Name = "Async User", Age = 27 };

            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.DEBUG);
            
            Console.WriteLine("📋 Processing async event with detailed logging...\n");

            var asyncEvent = new DemoUserRegistrationEvent(user, "async-demo");
            
            // First part: Initial dispatch with WAITING
            var success = asyncEvent.WithCallbacks<DemoUserRegistrationEvent>(dispatcher)
                .OnExecutePhase((evt, ctx) => {
                    LSEventLogger.Instance.Info("Starting async operation simulation");
                    
                    // Simulate async operation setup
                    evt.SetData("async.operation.started", DateTime.UtcNow);
                    evt.SetData("async.operation.type", "user_verification");
                    
                    LSEventLogger.Instance.Debug("Async operation configured, returning WAITING");
                    return LSHandlerResult.WAITING;
                })
                .OnSuccess(evt => {
                    LSEventLogger.Instance.Info("Async operation completed successfully");
                })
                .Dispatch();

            Console.WriteLine($"\n📊 Initial dispatch result (waiting): {success}");
            Console.WriteLine($"📊 Event waiting state: {asyncEvent.IsWaiting}");

            // Second part: Resume the async operation
            Console.WriteLine("\n⏳ Simulating async operation completion...\n");
            
            asyncEvent.SetData("async.operation.completed", DateTime.UtcNow);
            asyncEvent.Resume();

            Console.WriteLine($"\n📊 Final event state - Completed: {asyncEvent.IsCompleted}, Waiting: {asyncEvent.IsWaiting}");
            Console.WriteLine("📊 Above logs show the complete async operation lifecycle\n");
        }

        [TearDown]
        public void Cleanup() {
            // Reset to console provider for other tests
            LSEventLogger.Instance.ClearProviders();
            LSEventLogger.Instance.AddProvider(new LSConsoleLogProvider());
            LSEventLogger.Instance.MinimumLevel =(LSLogLevel.DEBUG);
        }
    }
}
