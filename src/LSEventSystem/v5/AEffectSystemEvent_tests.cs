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
        public int Armor { get; set; } = 1;

        public (int min, int max) Strength { get; set; } = (5, 10);
        public float HitChanceModifier { get; set; } = 0.8f; // 80% chance to hit
        public float EvasionModifier { get; set; } = 0.1f; // 10% chance to evade

        public Vector2 Position { get; set; } = new(0, 0);

        public Entity(string name) {
            Name = name;
            TargetMechanic = new TargetMechanic(this);
        }
        public TargetMechanic TargetMechanic { get; }

        public Guid ID { get; } = Guid.NewGuid();

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
        public abstract EffectTemplate[] GetSkillEffectTemplate();
    }
    public class PunchSkillTemplate : SkillTemplate {
        public override string SkillID => "basicPunch";
        public override string Label => "Punch";
        public override string Description => "A basic punch attack.";
        private DamageEffectTemplate? _damageEffect = null;
        protected PunchSkillTemplate() {
        }
        public override EffectTemplate[] GetSkillEffectTemplate() {
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
        public static Skill CreateSkill(Entity owner, int minDamage, int maxDamage, bool initialize = true) {
            var template = Create(minDamage, maxDamage);
            var skill = new Skill(owner, template);
            if (initialize) skill.Initialize();
            return skill;
        }
    }
    public class WaterballSkillTemplate : SkillTemplate {
        public override string SkillID => "waterball";
        public override string Label => "Waterball";
        public override string Description => "A ball of water that hits the target.";
        protected List<EffectTemplate> _effects = new();
        public WaterballSkillTemplate() {

        }
        public override EffectTemplate[] GetSkillEffectTemplate() {
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
            Console.WriteLine($"{Owner.Name} uses {Label} on {target.Name}.");
            foreach (var effectTemplate in SkillTemplate.GetSkillEffectTemplate()) {
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
        public abstract void Cleanup(Entity instance);
    }
    public class DamageEffectTemplate : EffectTemplate {
        public override string EffectID => "damageEffect";
        public (int min, int max) DamageAmount { get; }
        public DamageEffectTemplate(int minDamage, int maxDamage) {
            DamageAmount = (minDamage, maxDamage);
        }
        public override void Initialize(Entity instance) {
            // Register event context for OnEffectAppliedEvent to handle damage application

            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Selector($"{EffectID}", sel => sel
                    .Execute("rollDamage", (evt, ctx) => { //calculate how much damage the effect is going to deal
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        var source = appliedEffect.Source;
                        var target = appliedEffect.Target;
                        var rollStrength = new Random().Next(appliedEffect.Source.Strength.min, appliedEffect.Source.Strength.max + 1);
                        var rollDamage = new Random().Next(DamageAmount.min, DamageAmount.max + 1);
                        Console.WriteLine($"[{instance.Name}]: {source.Name} rolls {rollDamage} damage and {rollStrength} strength against {target.Name}'s {target.Armor} armor.");
                        evt.SetData("rollDamage", rollDamage);
                        evt.SetData("rollStrength", rollStrength);
                        evt.SetData("armor", target.Armor);
                        evt.SetData("currentDamage", Math.Max(0, rollDamage + rollStrength - target.Armor));

                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.HIGH, true)
                    // should always run last to ensure that the selector return success
                    .Execute("successRollDamage", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                    .Selector("applyDamage", sel => sel // apply the damage 
                        .Execute($"{instance.Name}", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            if (!evt.TryGetData<int>("currentDamage", out var damage)) {
                                Console.WriteLine($"[DamageEffectTemplate] No damage data found on event for {appliedEffect.Target.Name}.");
                                return LSEventProcessStatus.CANCELLED;
                            }
                            appliedEffect.Target.Health -= damage;
                            Console.WriteLine($"[{instance.Name}]: {appliedEffect.Target.Name} takes {damage} damage. Remaining Health: {appliedEffect.Target.Health}");
                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.NORMAL, true)
                        .Execute("successApplyDamage", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                    )
                ), instance);

        }
        public override void Cleanup(Entity instance) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{EffectID}"), instance);
        }
        public override void Execute(Effect sourceEffect) {
        }
    }
    public class ProtectEffectTemplate : EffectTemplate {
        public override string EffectID => "ProtectEffect";
        public int Duration { get; }
        public ProtectEffectTemplate(int duration) {
            Duration = duration;
        }
        public override void Initialize(Entity entity) {
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Selector($"onHitWatcher", sel => sel
                    .Execute($"{EffectID}_{entity.ID}_watcher", (evt, ctx) => {
                        if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                        if (onHitEvent.Target != entity) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"[{entity.Name}] protects [{onHitEvent.Target.Name}] from [{onHitEvent.Source.Name}]'s attack.");
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.HIGH)
                , LSPriority.HIGH), entity);
        }
        public override void Cleanup(Entity instance) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{EffectID}"), instance);
        }
        public override void Execute(Effect sourceEffect) {
            Console.WriteLine($"{sourceEffect.Source.Name} protects {sourceEffect.Target.Name}.");
            // var evt = new OnEffectAppliedEvent(sourceEffect);
            // evt.Process(sourceEffect.Target);
            // LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
            //     .Selector($"onHitWatcher", s => s
            //         .Execute($"{entity.Name}_watcher", (evt, ctx) => {
            //             if (evt is not OnHitEvent hitEvent) return LSEventProcessStatus.CANCELLED;

            //             return LSEventProcessStatus.SUCCESS;
            //         })
            //     ));

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
                .Sequence($"{EffectID}", s => s
                    .Execute("log", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{appliedEffect.Target.Name} is stunned for {Duration} turns by {appliedEffect.Source.Name}");
                        return LSEventProcessStatus.SUCCESS;
                    })
                )
            , entity);
        }
        public override void Cleanup(Entity instance) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{EffectID}"), instance);
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
                .Execute($"{EffectID}", (evt, ctx) => {
                    if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                    Console.WriteLine($"{appliedEffect.Source.Name} applied wet in {appliedEffect.Target.Name} for {Duration}.");
                    return LSEventProcessStatus.SUCCESS;
                })
            , entity);
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Selector($"{entity.Name}_watcher_selector", s => s
                    .Execute($"{entity.Name}_watcher", (evt, ctx) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{entity.Name} sees that {appliedEffect.Source.Name} applied wet on {appliedEffect.Target.Name}.");
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.BACKGROUND, false, (evt, node) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for wet watcher selector.");
                        // only trigger if the effect being applied is wet and the target is this entity
                        return appliedEffect.Effect.EffectID == EffectID;
                    })
                    .Execute("successWatcher", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                )
            );
        }
        public override void Cleanup(Entity instance) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{EffectID}"), instance);
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{instance.Name}_watcher_selector"));
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
        public string EffectID => EffectTemplate.EffectID;
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

    #endregion

    #region Target System
    public class TargetMechanic {
        private readonly Entity _owner;
        public TargetMechanic(Entity owner) {
            _owner = owner;
        }
        bool _isInitialized = false;
        public void Initialize(Entity entity) {
            if (_isInitialized) {
                throw new LSException($"TargetMechanic for {entity.Name} is already initialized.");
            }
            _isInitialized = true;
            Console.WriteLine($"Initializing TargetMechanic for {entity.Name}");
            // when the entity is the source of the hit event, it uses its equipped skill on the target
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Selector("onHitUseSkill", sel => sel
                    .Execute($"{_owner.Name}_skillUse", (evt, ctx) => {
                        if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                        Console.WriteLine($"{onHitEvent.Source.Name} has hit {onHitEvent.Target.Name} with {onHitEvent.Skill?.Label}.");
                        onHitEvent.Skill?.Use(onHitEvent.Target);
                        return LSEventProcessStatus.SUCCESS;
                    }, LSPriority.LOW) //the use skill node should be the last to run, this gives the change of other entities to react to the hit first (like protect)
                ), entity);

            // any entity that is not the source or target of the hit event can watch the event and react to it
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Selector("onHitWatcher", sel => sel // all entities watch the event, but only those that are not source or target will be able actually "watch" the onHitEvent
                    .Execute($"{_owner.Name}_watcher", (evt, ctx) => {
                        if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                        //if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return LSEventProcessStatus.SUCCESS;
                        Console.WriteLine($"{_owner.Name} saw {onHitEvent.Source.Name} hit {onHitEvent.Target.Name} with {onHitEvent.Skill?.Label}.");
                        return LSEventProcessStatus.SUCCESS; // we return success so that other watchers can also run, handler is inverted
                    }, LSPriority.LOW, true) //we give this handler a low priority so that it runs after other handlers (like protect)

                    .Execute("defaultPass", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND) // default pass
                , LSPriority.HIGH, false, true, (evt, node) => {
                    if (evt is not OnHitEvent onHitEvent) throw new LSException("Invalid event type for target selector.");
                    if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return false;
                    //Console.WriteLine($"Evaluating target selector for {_owner.Name} on event where {onHitEvent.Source.Name} hits {onHitEvent.Target.Name}");
                    return true;
                }));
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

        //var punchTemplate = PunchSkillTemplate.Create(1, 3);
        var protectTemplate = new ProtectEffectTemplate(2);

        entityA.Initialize(null!, null!);
        entityB.Initialize(null!, null!);
        entityC.Initialize(null!, null!);
        entityD.Initialize(null!, null!);

        entityA.EquippedSkill = PunchSkillTemplate.CreateSkill(entityA, minDamage: 1, maxDamage: 3, initialize: true);
        entityB.EquippedSkill = PunchSkillTemplate.CreateSkill(entityB, minDamage: 1, maxDamage: 3, initialize: true);
        entityC.EquippedSkill = WaterballSkillTemplate.Create(minDamage: 2, maxDamage: 5, wetDuration: 1).Instantiate(entityC).Initialize();
        entityD.EquippedSkill = PunchSkillTemplate.CreateSkill(entityD, minDamage: 1, maxDamage: 3, initialize: true);


        Assert.That(entityA.EquippedSkill != null);
        Assert.That(entityB.EquippedSkill != null);
        Assert.That(entityC.EquippedSkill != null);
        Assert.That(entityD.EquippedSkill != null);

        Console.WriteLine("---- Combat Start ----");
        entityA.Hit(entityB);

        entityA.Hit(entityB);

        entityA.Hit(entityB);

        entityA.Hit(entityB);
        //entityC.Hit(entityD);
    }
}

public static class Vector2Extensions {
    public static float DistanceTo(this Vector2 a, Vector2 b) {
        return Vector2.Distance(a, b);
    }
}
