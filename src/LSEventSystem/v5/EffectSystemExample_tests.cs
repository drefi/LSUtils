using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem.TestV5;

/// <summary>
/// Example test demonstrating an effect system using LSEventSystem v5.
/// Shows how entities can apply effects, react to effects, and handle priority-based claiming.
/// </summary>
[TestFixture]
public class EffectSystemExampleTests {

    #region Effect System Domain Models

    public enum EffectType {
        Wet,
        Burning,
        Frozen,
        Shocked
    }

    public class Effect {
        public EffectType Type { get; set; }
        public string AppliedBy { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public int Duration { get; set; } = 3; // turns
        public bool IsClaimed { get; set; } = false;
        public string? ClaimedBy { get; set; }
    }

    public class Skill {
        public string Name { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public EffectType? AppliesEffect { get; set; }
        public EffectType? ReactsToEffect { get; set; }
        public int Priority { get; set; } = 0; // higher number = higher priority
        public int Damage { get; set; } = 10;
    }

    public class Entity {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<Effect> ActiveEffects { get; set; } = new();
        public List<Skill> Skills { get; set; } = new();
        public int Health { get; set; } = 100;
        public bool IsAlive => Health > 0;

        public void ApplyEffect(Effect effect) {
            ActiveEffects.Add(effect);
        }

        public void RemoveEffect(EffectType effectType) {
            ActiveEffects.RemoveAll(e => e.Type == effectType);
        }

        public Effect? GetEffect(EffectType effectType) {
            return ActiveEffects.FirstOrDefault(e => e.Type == effectType);
        }

        public void TakeDamage(int damage, string source) {
            Health = Math.Max(0, Health - damage);
        }
    }

    #endregion

    #region Effect System Events

    public class SkillUsedEvent : LSEvent {
        public Entity Caster { get; }
        public Entity Target { get; }
        public Skill Skill { get; }

        public SkillUsedEvent(Entity caster, Entity target, Skill skill) {
            Caster = caster;
            Target = target;
            Skill = skill;
        }
    }

    public class EffectAppliedEvent : LSEvent {
        public Entity Target { get; }
        public Effect Effect { get; }
        public Entity AppliedBy { get; }

        public EffectAppliedEvent(Entity target, Effect effect, Entity appliedBy) {
            Target = target;
            Effect = effect;
            AppliedBy = appliedBy;
        }
    }

    public class EffectClaimedEvent : LSEvent {
        public Entity ClaimedBy { get; }
        public Entity EffectTarget { get; }
        public Effect Effect { get; }
        public Skill ReactiveSkill { get; }

        public EffectClaimedEvent(Entity claimedBy, Entity effectTarget, Effect effect, Skill reactiveSkill) {
            ClaimedBy = claimedBy;
            EffectTarget = effectTarget;
            Effect = effect;
            ReactiveSkill = reactiveSkill;
        }
    }

    #endregion

    #region Test Setup and Data

    private Entity _entityA = null!;
    private Entity _entityB = null!;
    private Entity _entityC = null!;
    private Entity _entityD = null!;
    private List<Entity> _allEntities = null!;
    private List<string> _actionLog = null!;

    [SetUp]
    public void Setup() {
        _actionLog = new List<string>();

        // Setup entities
        _entityA = new Entity {
            Id = "A",
            Name = "Aqua Mage",
            Skills = new List<Skill> {
                new Skill { Name = "Water Blast", OwnerId = "A", AppliesEffect = EffectType.Wet, Damage = 15 }
            }
        };

        _entityB = new Entity {
            Id = "B",
            Name = "Target Dummy",
            Skills = new List<Skill>()
        };

        _entityC = new Entity {
            Id = "C",
            Name = "Thunder Warrior",
            Skills = new List<Skill> {
                new Skill { Name = "Lightning Strike", OwnerId = "C", ReactsToEffect = EffectType.Wet, Priority = 10, Damage = 25 }
            }
        };

        _entityD = new Entity {
            Id = "D",
            Name = "Storm Caller",
            Skills = new List<Skill> {
                new Skill { Name = "Thunder Bolt", OwnerId = "D", ReactsToEffect = EffectType.Wet, Priority = 5, Damage = 20 }
            }
        };

        _allEntities = new List<Entity> { _entityA, _entityB, _entityC, _entityD };

        // Setup the event system contexts
        SetupEffectSystemContexts();
    }

    [TearDown]
    public void Cleanup() {
        // Reset all entity states
        foreach (var entity in _allEntities) {
            entity.Health = 100;
            entity.ActiveEffects.Clear();
        }
        _actionLog.Clear();
    }

    private void SetupEffectSystemContexts() {
        // Register global contexts for effect system

        // Context for SkillUsedEvent - handles damage and effect application
        var skillUsedContext = new LSEventContextBuilder()
            .Sequence("skill-processing", seq => seq
                .Execute("apply-damage", (evt, node) => {
                    var skillEvent = evt as SkillUsedEvent;
                    if (skillEvent != null) {
                        skillEvent.Target.TakeDamage(skillEvent.Skill.Damage, skillEvent.Caster.Id);
                        _actionLog.Add($"{skillEvent.Caster.Name} uses {skillEvent.Skill.Name} on {skillEvent.Target.Name} for {skillEvent.Skill.Damage} damage");
                        return LSEventProcessStatus.SUCCESS;
                    }
                    return LSEventProcessStatus.FAILURE;
                }, LSPriority.HIGH)
                .Execute("apply-effect", (evt, node) => {
                    var skillEvent = evt as SkillUsedEvent;
                    if (skillEvent?.Skill.AppliesEffect.HasValue == true) {
                        var effect = new Effect {
                            Type = skillEvent.Skill.AppliesEffect.Value,
                            AppliedBy = skillEvent.Caster.Id
                        };
                        skillEvent.Target.ApplyEffect(effect);
                        _actionLog.Add($"{skillEvent.Target.Name} is now affected by {effect.Type}");
                        
                        // Fire effect applied event to trigger reactions
                        var effectEvent = new EffectAppliedEvent(skillEvent.Target, effect, skillEvent.Caster);
                        effectEvent.Process();
                    }
                    return LSEventProcessStatus.SUCCESS;
                }, LSPriority.NORMAL))
            .Build();

        LSEventContextManager.Singleton.RegisterContext<SkillUsedEvent>(skillUsedContext);

        // Context for EffectAppliedEvent - handles reactive skills with priority
        var effectAppliedContext = new LSEventContextBuilder()
            .Sequence("effect-reactions", seq => seq
                .Execute("find-reactors", (evt, node) => {
                    var effectEvent = evt as EffectAppliedEvent;
                    if (effectEvent != null) {
                        var reactors = FindPotentialReactors(effectEvent.Effect, effectEvent.Target);
                        evt.SetData("reactors", reactors);
                        _actionLog.Add($"Found {reactors.Count} potential reactors to {effectEvent.Effect.Type} on {effectEvent.Target.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    }
                    return LSEventProcessStatus.FAILURE;
                }, LSPriority.CRITICAL)
                .Execute("process-reactions", (evt, node) => {
                    var effectEvent = evt as EffectAppliedEvent;
                    if (effectEvent != null && evt.TryGetData<List<(Entity, Skill)>>("reactors", out var reactors)) {
                        ProcessReactions(effectEvent, reactors);
                        return LSEventProcessStatus.SUCCESS;
                    }
                    return LSEventProcessStatus.FAILURE;
                }, LSPriority.NORMAL))
            .Build();

        LSEventContextManager.Singleton.RegisterContext<EffectAppliedEvent>(effectAppliedContext);

        // Context for EffectClaimedEvent - logs the claim
        var effectClaimedContext = new LSEventContextBuilder()
            .Sequence("claim-processing", seq => seq
                .Execute("log-claim", (evt, node) => {
                    var claimEvent = evt as EffectClaimedEvent;
                    if (claimEvent != null) {
                        _actionLog.Add($"{claimEvent.ClaimedBy.Name} claims {claimEvent.Effect.Type} effect on {claimEvent.EffectTarget.Name} with {claimEvent.ReactiveSkill.Name}");
                        
                        // Apply the reactive skill effect
                        claimEvent.EffectTarget.TakeDamage(claimEvent.ReactiveSkill.Damage, claimEvent.ClaimedBy.Id);
                        _actionLog.Add($"{claimEvent.EffectTarget.Name} takes {claimEvent.ReactiveSkill.Damage} additional damage from {claimEvent.ReactiveSkill.Name}");
                        
                        return LSEventProcessStatus.SUCCESS;
                    }
                    return LSEventProcessStatus.FAILURE;
                }))
            .Build();

        LSEventContextManager.Singleton.RegisterContext<EffectClaimedEvent>(effectClaimedContext);
    }

    private List<(Entity, Skill)> FindPotentialReactors(Effect effect, Entity target) {
        var reactors = new List<(Entity, Skill)>();
        
        foreach (var entity in _allEntities) {
            if (entity == target || !entity.IsAlive) continue;
            
            foreach (var skill in entity.Skills) {
                if (skill.ReactsToEffect == effect.Type) {
                    reactors.Add((entity, skill));
                }
            }
        }
        
        // Sort by priority (highest first)
        return reactors.OrderByDescending(r => r.Item2.Priority).ToList();
    }

    private void ProcessReactions(EffectAppliedEvent effectEvent, List<(Entity, Skill)> reactors) {
        var effect = effectEvent.Target.GetEffect(effectEvent.Effect.Type);
        if (effect == null || effect.IsClaimed) return;

        foreach (var (reactor, skill) in reactors) {
            if (effect.IsClaimed) break; // Already claimed by higher priority reactor

            // Claim the effect
            effect.IsClaimed = true;
            effect.ClaimedBy = reactor.Id;

            // Fire claim event
            var claimEvent = new EffectClaimedEvent(reactor, effectEvent.Target, effect, skill);
            claimEvent.Process();
            
            break; // Only the highest priority reactor gets to claim
        }
    }

    #endregion

    #region Test Cases

    [Test]
    public void TestBasicEffectSystem_EntityAAppliesWetToEntityB() {
        // Arrange
        var waterBlast = _entityA.Skills.First();
        var initialHealthB = _entityB.Health;

        // Act
        var skillEvent = new SkillUsedEvent(_entityA, _entityB, waterBlast);
        var result = skillEvent.Process();

        // Assert
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        
        // EntityB should have taken damage from both Water Blast and Lightning Strike (reaction)
        var expectedDamage = waterBlast.Damage + _entityC.Skills.First().Damage; // 15 + 25 = 40
        Assert.That(_entityB.Health, Is.EqualTo(initialHealthB - expectedDamage));
        Assert.That(_entityB.GetEffect(EffectType.Wet), Is.Not.Null);
        Assert.That(_actionLog.Count, Is.GreaterThan(0));
        
        // Check action log includes basic skill and reactive behavior
        Assert.That(_actionLog.Any(log => log.Contains("Water Blast")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("Wet")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("Thunder Warrior claims")), Is.True);
    }

    [Test]
    public void TestEffectReaction_EntityCReactsToWetWithHigherPriority() {
        // Arrange
        var waterBlast = _entityA.Skills.First();
        var initialHealthB = _entityB.Health;

        // Act
        var skillEvent = new SkillUsedEvent(_entityA, _entityB, waterBlast);
        skillEvent.Process();

        // Assert
        var wetEffect = _entityB.GetEffect(EffectType.Wet);
        Assert.That(wetEffect, Is.Not.Null);
        Assert.That(wetEffect!.IsClaimed, Is.True);
        Assert.That(wetEffect.ClaimedBy, Is.EqualTo("C")); // EntityC has higher priority (10)
        
        // EntityB should have taken damage from both the original skill and the reaction
        var expectedDamage = waterBlast.Damage + _entityC.Skills.First().Damage;
        Assert.That(_entityB.Health, Is.EqualTo(initialHealthB - expectedDamage));
        
        // Check action log for reaction
        Assert.That(_actionLog.Any(log => log.Contains("Thunder Warrior claims")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("Lightning Strike")), Is.True);
    }

    [Test]
    public void TestEffectPriority_EntityCClaimsBeforeEntityD() {
        // Arrange
        var waterBlast = _entityA.Skills.First();

        // Act
        var skillEvent = new SkillUsedEvent(_entityA, _entityB, waterBlast);
        skillEvent.Process();

        // Assert
        var wetEffect = _entityB.GetEffect(EffectType.Wet);
        Assert.That(wetEffect, Is.Not.Null);
        Assert.That(wetEffect!.IsClaimed, Is.True);
        Assert.That(wetEffect.ClaimedBy, Is.EqualTo("C")); // EntityC (priority 10) should claim before EntityD (priority 5)
        
        // Verify that EntityD did not get to react
        Assert.That(_actionLog.Any(log => log.Contains("Storm Caller")), Is.False);
        Assert.That(_actionLog.Any(log => log.Contains("Thunder Bolt")), Is.False);
        
        // But EntityC did react
        Assert.That(_actionLog.Any(log => log.Contains("Thunder Warrior")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("Lightning Strike")), Is.True);
    }

    [Test]
    public void TestCompleteScenario_FullWorkflow() {
        // Arrange
        var initialHealthB = _entityB.Health;
        _actionLog.Clear();

        // Act - EntityA uses Water Blast on EntityB
        var skillEvent = new SkillUsedEvent(_entityA, _entityB, _entityA.Skills.First());
        var result = skillEvent.Process();

        // Assert - Complete workflow verification
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        
        // 1. EntityB took damage from Water Blast
        Assert.That(_entityB.Health, Is.LessThan(initialHealthB));
        
        // 2. EntityB has Wet effect
        var wetEffect = _entityB.GetEffect(EffectType.Wet);
        Assert.That(wetEffect, Is.Not.Null);
        Assert.That(wetEffect!.Type, Is.EqualTo(EffectType.Wet));
        Assert.That(wetEffect.AppliedBy, Is.EqualTo("A"));
        
        // 3. Effect was claimed by EntityC (higher priority)
        Assert.That(wetEffect.IsClaimed, Is.True);
        Assert.That(wetEffect.ClaimedBy, Is.EqualTo("C"));
        
        // 4. EntityB took additional damage from Lightning Strike
        var expectedTotalDamage = 15 + 25; // Water Blast + Lightning Strike
        Assert.That(_entityB.Health, Is.EqualTo(initialHealthB - expectedTotalDamage));
        
        // 5. Verify action log sequence
        var expectedActions = new[] {
            "Aqua Mage uses Water Blast",
            "Target Dummy is now affected by Wet",
            "Found 2 potential reactors",
            "Thunder Warrior claims Wet effect",
            "Target Dummy takes 25 additional damage"
        };
        
        foreach (var expectedAction in expectedActions) {
            Assert.That(_actionLog.Any(log => log.Contains(expectedAction.Split(' ')[0])), Is.True, 
                $"Expected action containing '{expectedAction.Split(' ')[0]}' not found in log");
        }
        
        // 6. EntityD should not have reacted due to lower priority
        Assert.That(_actionLog.Any(log => log.Contains("Storm Caller")), Is.False);
    }

    [Test]
    public void TestContextBuilderFluentAPI_CustomEventContext() {
        // Test using fluent API to create a custom context for a specific event instance
        var customEvent = new SkillUsedEvent(_entityA, _entityB, _entityA.Skills.First());
        
        var result = customEvent
            .Context(builder => builder
                .Sequence("custom-skill-processing", seq => seq
                    .Execute("log-start", (evt, node) => {
                        _actionLog.Add("Custom processing started");
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.CRITICAL)
                    .Execute("apply-custom-effect", (evt, node) => {
                        var skillEvt = evt as SkillUsedEvent;
                        if (skillEvt != null) {
                            var customEffect = new Effect {
                                Type = EffectType.Burning,
                                AppliedBy = "CUSTOM"
                            };
                            skillEvt.Target.ApplyEffect(customEffect);
                            _actionLog.Add("Custom burning effect applied");
                        }
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.NORMAL)))
            .Process();
        
        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_actionLog.Any(log => log.Contains("Custom processing started")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("Custom burning effect applied")), Is.True);
        Assert.That(_entityB.GetEffect(EffectType.Burning), Is.Not.Null);
    }

    [Test]
    public void TestParallelProcessing_MultipleReactorsIfEffectNotClaimed() {
        // Modify the effect to not be immediately claimed to test parallel reactions
        var modifiedEffectContext = new LSEventContextBuilder()
            .Parallel("parallel-reactions", par => par
                .Execute("reactor-c", (evt, node) => {
                    _actionLog.Add("EntityC processing in parallel");
                    return LSEventProcessStatus.SUCCESS;
                }, LSPriority.HIGH)
                .Execute("reactor-d", (evt, node) => {
                    _actionLog.Add("EntityD processing in parallel");
                    return LSEventProcessStatus.SUCCESS;
                }, LSPriority.NORMAL), 2) // Both must succeed
            .Build();

        // Create a custom event with parallel processing context
        var effectEvent = new EffectAppliedEvent(_entityB, new Effect { Type = EffectType.Wet }, _entityA);
        var result = effectEvent
            .Context(builder => builder.Merge(modifiedEffectContext))
            .Process();

        Assert.That(result, Is.EqualTo(LSEventProcessStatus.SUCCESS));
        Assert.That(_actionLog.Any(log => log.Contains("EntityC processing in parallel")), Is.True);
        Assert.That(_actionLog.Any(log => log.Contains("EntityD processing in parallel")), Is.True);
    }

    #endregion

    #region Helper Methods

    private void PrintActionLog() {
        Console.WriteLine("=== Action Log ===");
        for (int i = 0; i < _actionLog.Count; i++) {
            Console.WriteLine($"{i + 1}. {_actionLog[i]}");
        }
        Console.WriteLine("==================");
    }

    #endregion
}
