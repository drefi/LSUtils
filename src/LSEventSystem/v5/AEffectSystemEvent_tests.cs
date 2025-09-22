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
            TargetMechanic = new TargetMechanic(this);
        }
        public TargetMechanic TargetMechanic { get; }

        public string InstanceID { get; } = Guid.NewGuid().ToString();

        public LSEventProcessStatus Initialize(LSEventContextManager manager, ILSEventLayerNode context) {
            TargetMechanic?.Initialize(this);
            return LSEventProcessStatus.SUCCESS;
        }
        public void Hit(Entity target) {
            if (EquippedSkill == null) {
                Console.WriteLine($"[Entity.Hit]: {Name} has no skill equipped.");
                return;
            }
            var evt = new OnHitEvent(this, target, EquippedSkill);
            evt.Process(target);
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
        public abstract string Label { get; }
        public abstract string Description { get; }
        public Skill Instantiate(Entity owner) {
            return new Skill(owner, this);
        }
        public abstract void Initialize(Entity entity);
        public abstract EffectTemplate[] GetEffects();
    }
    public class PunchSkillTemplate : SkillTemplate {
        public override string SkillID => "basicPunch";
        public override string Label => "Punch";
        public override string Description => "A basic punch attack.";
        private DamageEffectTemplate? _damageEffect = null;
        protected PunchSkillTemplate() {
        }
        public override EffectTemplate[] GetEffects() {
            return _damageEffect != null ? new EffectTemplate[] { _damageEffect } : Array.Empty<EffectTemplate>();
        }
        public override void Initialize(Entity entity) {
            _damageEffect?.Initialize(entity);
        }
        public static PunchSkillTemplate Create(int minDamage, int maxDamage) {
            return new PunchSkillTemplate() {
                _damageEffect = new DamageEffectTemplate(minDamage, maxDamage)
            };
        }
    }
    public class WaterballSkillTemplate : SkillTemplate {
        public override string SkillID => "waterball";
        public override string Label => "Waterball";
        public override string Description => "A ball of water that hits the target.";
        protected List<EffectTemplate> _effects = new();
        public WaterballSkillTemplate() {

        }
        public override EffectTemplate[] GetEffects() {
            return _effects.ToArray();
        }
        public override void Initialize(Entity entity) {
            //_damageEffect.Initialize(entity);
            foreach (var effect in _effects) {
                effect.Initialize(entity);
            }
        }
        public static WaterballSkillTemplate Create(int minDamage, int maxDamage, int wetDuration) {
            var template = new WaterballSkillTemplate();
            template._effects.Add(new DamageEffectTemplate(minDamage, maxDamage));
            template._effects.Add(new WetEffectTemplate(wetDuration));
            return template;
        }
    }


    public class Skill {
        public string Label => SkillTemplate.Label;
        public string Description => SkillTemplate.Description;
        public string SkillID => SkillTemplate.SkillID;
        public Guid ID { get; } = Guid.NewGuid();
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
    public class WetEffectTemplate : EffectTemplate {
        public override string EffectID => "wetEffect";
        public int Duration { get; }
        public WetEffectTemplate(int duration) {
            Duration = duration;
        }
        public override void Initialize(Entity entity) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Execute("instance", (evt, ctx) => {
                    if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                    Console.WriteLine($"{appliedEffect.Source.Name} applied wet in {appliedEffect.Target.Name} for {Duration}.");
                    return LSEventProcessStatus.SUCCESS;
                })
            , entity);
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Selector("onWet", s => s
                    .Execute($"{entity.Name}", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{entity.Name} sees that {appliedEffect.Target.Name} is wet.");
                        return LSEventProcessStatus.SUCCESS;
                    })
                )
            );
        }
        public override void Execute(Effect sourceEffect) {
            Console.WriteLine($"{sourceEffect.Target.Name} is wet for {Duration} turns by {sourceEffect.Source.Name}");
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
        private readonly Entity _owner;
        public TargetMechanic(Entity owner) {
            _owner = owner;
        }
        public void Initialize(Entity entity) {
            //Console.WriteLine($"Initializing TargetMechanic for {entity.Name}");
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Sequence("onHit", root => root
                    .Execute("skill", (evt, ctx) => {
                        if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{onHitEvent.Source.Name} has hit {onHitEvent.Target.Name} with {onHitEvent.Skill?.Label}.");
                        return LSEventProcessStatus.SUCCESS;
                    })
                ), entity);
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Selector("target", root => root
                    .Execute($"{_owner.Name}", (evt, ctx) => {
                        if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                        //if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return LSEventProcessStatus.SUCCESS;
                        Console.WriteLine($"{_owner.Name} saw {onHitEvent.Source.Name} hit {onHitEvent.Target.Name} with {onHitEvent.Skill?.Label}.");
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.NORMAL, true, (evt, node) => {
                        if (evt is not OnHitEvent onHitEvent) throw new LSException("Invalid event type for target selector.");
                        if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return false;
                        Console.WriteLine($"Evaluating target selector for {_owner.Name} on event where {onHitEvent.Source.Name} hits {onHitEvent.Target.Name}");
                        return true;
                    })
                    .Execute("defaulPass", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND) // default pass
                ));
        }
    }
    public class OnHitEvent : LSEvent {
        public Entity Source { get; }
        public Entity Target { get; }
        public Skill Skill { get; }
        public OnHitEvent(Entity source, Entity target, Skill skill) {
            Source = source;
            Target = target;
            Skill = skill;
        }

    }
    #endregion

    [Test]
    public void TestOnTargetEvent() {
        var entityA = new Entity("Entity A");
        var entityB = new Entity("Entity B");
        var entityC = new Entity("Entity C");
        var entityD = new Entity("Entity D");


        //OnTargettedEvent.TargettedSystem();

        var punchTemplate = PunchSkillTemplate.Create(1, 3);
        var waterBallTemplate = WaterballSkillTemplate.Create(2, 5, 1);
        var protectTemplate = new ProtectEffectTemplate(2);

        entityA.Initialize(null!, null!);
        entityB.Initialize(null!, null!);
        entityC.Initialize(null!, null!);
        entityD.Initialize(null!, null!);

        entityA.EquippedSkill = punchTemplate.Instantiate(entityA).Initialize();
        entityB.EquippedSkill = punchTemplate.Instantiate(entityB).Initialize();
        entityC.EquippedSkill = waterBallTemplate.Instantiate(entityC).Initialize();
        entityD.EquippedSkill = punchTemplate.Instantiate(entityD).Initialize();


        Assert.That(entityA.EquippedSkill != null);
        Assert.That(entityB.EquippedSkill != null);
        Assert.That(entityC.EquippedSkill != null);
        Assert.That(entityD.EquippedSkill != null);

        Console.WriteLine("---- Combat Start ----");
        entityA.Hit(entityB);
        entityC.Hit(entityD);
    }
}

public static class Vector2Extensions {
    public static float DistanceTo(this Vector2 a, Vector2 b) {
        return Vector2.Distance(a, b);
    }
}
