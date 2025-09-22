using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace LSUtils.EventSystem.EffectTests;

[TestFixture]
public class AEffectSystemEvent_tests {

    public class EntityManager {
        public List<Entity> Entities = new();
        public void AddEntity(Entity e) => Entities.Add(e);
        public void RemoveEntity(Entity e) => Entities.Remove(e);
        public bool CanHit(Entity source, Entity target) {
            // Implement hit logic here
            return true;
        }
        public void Select(Entity entity, Entity other) {

        }

    }

    #region Entity System

    public class Entity : ILSEventable {
        public string Name { get; }
        public List<Effect> Effects = new();
        public Skill? EquippedSkill { get; set; }
        public int Health { get; set; } = 100;
        public int Mana { get; set; } = 100;
        public int Armor { get; set; } = 0;

        public (int min, int max) Strength { get; set; } = (5, 10);
        public float HitChanceModifier { get; set; } = 0.8f; // 80% chance to hit
        public float EvasionModifier { get; set; } = 0.1f; // 10% chance to evade

        public Vector2 Position { get; set; } = new(0, 0);

        public Entity(string name) {
            Name = name;
            TargetMechanic = new TargetMechanic();
        }
        public TargetMechanic TargetMechanic { get; }

        public LSEventProcessStatus Initialize(LSEventContextManager manager, ILSEventLayerNode context) {
            TargetMechanic?.Initialize(this);
            return LSEventProcessStatus.SUCCESS;
        }
        public void Target(Entity target) {
            Console.WriteLine($"{Name} is targeting {target.Name}");
            var evt = new OnTargetEvent(this, target);
            evt.Process(target);
        }
        public void Hit(Skill source) {

        }
        public void ApplyEffect(Effect effect) {
            var evt = new OnEffectAppliedEvent(effect);
            evt.Process(this);
        }
    }
    #endregion

    #region Skill System
    public abstract class SkillTemplate {
        public abstract string SkillID { get; }
        public Skill Instantiate(Entity owner) {
            return new Skill(owner, this);
        }
        public abstract void Initialize(Entity entity);
        public abstract EffectTemplate[] GetEffects();
    }
    public class PunchSkillTemplate : SkillTemplate {
        public override string SkillID => "Punch";
        private DamageEffectTemplate _damageEffect;
        public PunchSkillTemplate() {
            _damageEffect = new DamageEffectTemplate(8, 12);
        }
        public override EffectTemplate[] GetEffects() {
            return new EffectTemplate[] { _damageEffect };
        }
        public override void Initialize(Entity entity) {
            _damageEffect.Initialize(entity);
        }
    }


    public class Skill {
        //public readonly List<Effect> Effects = new();
        public Entity Owner { get; }
        public SkillTemplate SkillTemplate { get; }
        public virtual float Accuracy => 1f; // 100% chance to hit

        public Skill(Entity owner, SkillTemplate template) {
            Owner = owner;
            SkillTemplate = template;
        }
        public void Use(Entity target) {

            foreach (var effectTemplate in SkillTemplate.GetEffects()) {
                var effect = effectTemplate.Instantiate(Owner, target);
                target.ApplyEffect(effect);
            }
        }
        public Skill Initialize() {
            Console.WriteLine($"Initializing skill {SkillTemplate.SkillID} for {Owner.Name}");
            SkillTemplate.Initialize(Owner);
            return this;
        }
    }

    #endregion

    #region Effect System
    public abstract class EffectTemplate {
        public abstract string EffectID { get; }

        public abstract void Execute(Effect sourceEffect);
        public virtual Effect Instantiate(Entity source, Entity target) {
            return new Effect(source, target, this);
        }
        public abstract void Initialize(Entity instance);
    }
    public class DamageEffectTemplate : EffectTemplate {
        public override string EffectID => "DamageEffect";
        public (int min, int max) DamageAmount { get; }
        public DamageEffectTemplate(int minDamage, int maxDamage) {
            DamageAmount = (minDamage, maxDamage);
        }
        public override void Initialize(Entity instance) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence("OnTakeDamage", s => s
                    .Execute("log", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{appliedEffect.Target.Name} is taking damage from {appliedEffect.Source.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                    .Execute("applyDamage", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        var target = appliedEffect.Target;
                        var damage = appliedEffect.GetData<int>("damageAmount");
                        target.Health -= damage;
                        Console.WriteLine($"{target.Name} takes {damage} damage, remaining health: {target.Health}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                ), instance);
        }
        public override void Execute(Effect sourceEffect) {

            var sourceStrength = sourceEffect.Source.Strength;
            var strengthValue = new Random().Next(sourceStrength.min, sourceStrength.max + 1);
            var damageValue = new Random().Next(DamageAmount.min, DamageAmount.max + 1);
            Console.WriteLine($"{sourceEffect.Source.Name} deals {damageValue + strengthValue} damage to {sourceEffect.Target.Name} (Base: {damageValue}, Strength: {strengthValue})");
            var evt = new OnEffectAppliedEvent(sourceEffect);
            evt.SetData("damageValue", damageValue);
            evt.SetData("strengthValue", strengthValue);
            evt.SetData("damageAmount", damageValue + strengthValue);
            evt.Process(sourceEffect.Target);
            // sourceEffect.Target.Health -= damageValue + strengthValue;
        }
    }
    public class ProtectEffectTemplate : EffectTemplate {
        public override string EffectID => "ProtectEffect";
        public int Duration { get; }
        public ProtectEffectTemplate(int duration) {
            Duration = duration;
        }
        public override void Initialize(Entity entity) {

            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence("OnProtected", s => s
                    .Execute("redirect", redirectNodeHandler)
                ), entity);
        }
        LSEventProcessStatus redirectNodeHandler(ILSEvent evt, LSEventProcessContext context) {
            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
            Console.WriteLine($"{appliedEffect.Target.Name} is being protected by {appliedEffect.Source.Name}");

            var redirectEvent = new OnTargetEvent(appliedEffect.Target, appliedEffect.Source);
            return LSEventProcessStatus.SUCCESS;
        }
        public override void Execute(Effect sourceEffect) {
            Console.WriteLine($"{sourceEffect.Target.Name} is being protected by {sourceEffect.Source.Name} for {Duration} turns.");
            var evt = new OnEffectAppliedEvent(sourceEffect);
            evt.Process(sourceEffect.Target);
        }
    }
    public class StunEffectTemplate : EffectTemplate {
        public override string EffectID => "StunEffect";
        public int Duration { get; }
        public StunEffectTemplate(int duration) {
            Duration = duration;
        }
        public override void Initialize(Entity entity) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence("OnStunned", s => s
                    .Execute("log", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{appliedEffect.Target.Name} is stunned for {Duration} turns by {appliedEffect.Source.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                )
            , entity);
        }
        public override void Execute(Effect sourceEffect) {
            Console.WriteLine($"{sourceEffect.Target.Name} is stunned for {Duration} turns by {sourceEffect.Source.Name}");
            var evt = new OnEffectAppliedEvent(sourceEffect);
            evt.Process();
        }
    }

    #region Effect Instance
    public class Effect {
        public Guid ID { get; }
        public Entity Source { get; }
        public Entity Target { get; }
        public EffectTemplate EffectTemplate { get; }
        public Effect(Entity source, Entity target, EffectTemplate effectTemplate) {
            ID = Guid.NewGuid();
            Source = source;
            Target = target;
            EffectTemplate = effectTemplate;
        }
        public void Execute() {
            EffectTemplate.Execute(this);
        }

    }

    public class OnEffectAppliedEvent : LSEvent {
        public Entity Source { get; }
        public Entity Target { get; }
        public Effect Effect { get; }
        public OnEffectAppliedEvent(Effect effect) {
            Target = effect.Target;
            Source = effect.Source;
            Effect = effect;
        }
    }
    #endregion
    // public class OnStunnedEvent : LSEvent {
    //     public Entity Instance { get; }
    //     public Entity Source { get; }
    //     public int Duration { get; }
    //     public OnStunnedEvent(Entity instance, Entity source, int duration) {
    //         Instance = instance;
    //         Source = source;
    //         Duration = duration;
    //     }
    // }


    #endregion

    #region Target System
    public class TargetMechanic {
        public void Initialize(Entity entity) {
            Console.WriteLine($"Initializing TargetMechanic for {entity.Name}");
            LSEventContextManager.Singleton.Register<OnTargetEvent>(root => root
                .Sequence("OnTarget", s => s
                    .Execute("log", (evt, ctx) => {
                        Console.WriteLine($"{entity.Name} has triggered [log] handler.");
                        if (evt is not OnTargetEvent onTargettedEvent) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{onTargettedEvent.Source.Name} has targeted {onTargettedEvent.Target.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                    .Execute("select", (evt, ctx) => {
                        Console.WriteLine($"{entity.Name} has triggered [select] handler.");
                        if (evt is not OnTargetEvent onTargettedEvent) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{onTargettedEvent.Source.Name} is selecting {onTargettedEvent.Target.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                ), entity);
            var test = LSEventContextManager.Singleton.GetContext<OnTargetEvent>(null, entity);
            Console.WriteLine($"Context for {entity.Name}: {test.NodeID} with {test.GetChildren().Count()} children.");
        }
    }
    public class OnTargetEvent : LSEvent {
        public Entity Source { get; }
        public Entity Target { get; }
        public OnTargetEvent(Entity source, Entity target) {
            Source = source;
            Target = target;
        }

    }
    // public class OnHitEvent : LSEvent {
    //     public Entity Instance { get; }
    //     public Skill Source { get; }
    //     public OnHitEvent(Entity instance, Skill source) {
    //         Source = source;
    //         Instance = instance;
    //     }
    // }
    // public class OnTakeDamageEvent : LSEvent {
    //     public Entity Instance { get; }
    //     public Entity Source { get; }
    //     public int DamageAmount { get; }
    //     public OnTakeDamageEvent(Entity instance, Entity source, int damageAmount) {
    //         Instance = instance;
    //         Source = source;
    //         SetData<int>("DamageAmount", damageAmount);
    //         DamageAmount = damageAmount;
    //     }
    // }
    #endregion

    [Test]
    public void TestOnTargetEvent() {
        var entityA = new Entity("Entity A");
        var entityB = new Entity("Entity B");
        var entityC = new Entity("Entity C");
        var entityD = new Entity("Entity D");


        //OnTargettedEvent.TargettedSystem();

        var punchTemplate = new PunchSkillTemplate();

        entityA.Initialize(null!, null!);
        entityB.Initialize(null!, null!);
        entityC.Initialize(null!, null!);
        entityD.Initialize(null!, null!);

        entityA.EquippedSkill = punchTemplate.Instantiate(entityA).Initialize();
        entityB.EquippedSkill = punchTemplate.Instantiate(entityB).Initialize();
        entityC.EquippedSkill = punchTemplate.Instantiate(entityC).Initialize();
        entityD.EquippedSkill = punchTemplate.Instantiate(entityD).Initialize();


        Assert.That(entityA.EquippedSkill != null);
        Assert.That(entityB.EquippedSkill != null);
        Assert.That(entityC.EquippedSkill != null);
        Assert.That(entityD.EquippedSkill != null);

        Console.WriteLine("---- Combat Start ----");
        entityA.Target(entityB);
        entityC.Target(entityD);
    }
}

public static class Vector2Extensions {
    public static float DistanceTo(this Vector2 a, Vector2 b) {
        return Vector2.Distance(a, b);
    }
}
