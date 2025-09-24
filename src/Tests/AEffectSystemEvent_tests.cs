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

        public LSEventProcessStatus Initialize(LSEventContextDelegate? ctxBuilder = null, LSEventContextManager? manager = null) {
            TargetMechanic?.Initialize();
            return LSEventProcessStatus.SUCCESS;
        }
        public void Hit(Entity target) {
            Console.WriteLine($"[Entity.Hit]: {Name} attempts to hit {target.Name}.");
            if (EquippedSkill == null) {
                Console.WriteLine($"[Entity.Hit]: {Name} has no skill equipped.");
                return;
            }
            var evt = new OnHitEvent(this, target, EquippedSkill);
            evt.Process(target);
        }
        public void ApplyEffect(Effect effect) {
            Console.WriteLine($"[Entity.ApplyEffect]: {Name} is having effect {effect.EffectTemplate.EffectID} applied.");
            effect.Apply();
            var evt = new OnEffectAppliedEvent(effect);
            evt.Process(this);
        }
        public void Update() {
            // Trigger effect updates through the event system
            var evt = new OnEffectUpdateEvent(this);
            evt.Process(this);
        }
    }
    #endregion

    #region Skill System
    public abstract class SkillTemplate {
        public static SkillTemplate Singleton => throw new LSNotImplementedException("Singleton not implemented for SkillTemplate");
        public System.Guid ID { get; } = Guid.NewGuid();
        public abstract string SkillID { get; }
        public abstract string ClassName { get; }
        public abstract string Label { get; }
        public abstract string Description { get; }

        protected EffectTemplate[] _effects;

        protected SkillTemplate(EffectTemplate[] effects) {
            _effects = effects.ToArray(); //force copy
        }
        public abstract Skill Instantiate(Entity owner, params object[] args);
        public abstract void Initialize();

        public abstract EffectTemplate[] GetSkillEffectTemplate();
        public abstract Effect[] GetEffects(Skill source, Entity target);
    }
    public class PunchSkillTemplate : SkillTemplate {
        static PunchSkillTemplate? _instance;
        public new static PunchSkillTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new PunchSkillTemplate(new EffectTemplate[] {
                        DamageEffectTemplate.Singleton
                    });
                    _instance.Initialize();
                }
                return _instance;
            }
        }
        public override string SkillID => "basicPunch";
        public override string ClassName => typeof(PunchSkillTemplate).AssemblyQualifiedName ?? nameof(PunchSkillTemplate);
        Dictionary<Entity, Skill> _entitySkills = new();
        public override string Label => "Punch";
        public override string Description => "A basic punch attack.";
        public (int min, int max) BaseDamage => (1, 3);
        protected PunchSkillTemplate(EffectTemplate[] effects) : base(effects) {

        }
        public override Skill Instantiate(Entity owner, params object[] args) {
            var skill = new Skill(owner, this);
            return skill;
        }

        static bool _initialized = false;
        public override void Initialize() {
            if (_initialized) {
                Console.WriteLine($"[PunchSkillTemplate] {SkillID} is already initialized.");
                return;
            }
            _initialized = true;
            //NOTE: EffectTemplate initialization is called when the effect template is created.
        }
        public override Effect[] GetEffects(Skill source, Entity target) {
            var effectInstances = new Effect[] {
                DamageEffectTemplate.Singleton.Instantiate(source, target, BaseDamage.min, BaseDamage.max)
            };

            return effectInstances.ToArray();
        }
        public override EffectTemplate[] GetSkillEffectTemplate() {
            return _effects.ToArray();
        }
    }
    public class WaterballSkillTemplate : SkillTemplate {
        static WaterballSkillTemplate? _instance;
        public new static WaterballSkillTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new WaterballSkillTemplate(new EffectTemplate[] {
                        DamageEffectTemplate.Singleton,
                        WetEffectTemplate.Singleton
                    });
                    _instance.Initialize();
                }
                return _instance;
            }
        }
        public override string SkillID => "waterball";
        public override string ClassName => typeof(WaterballSkillTemplate).AssemblyQualifiedName ?? nameof(WaterballSkillTemplate);
        public override string Label => "Waterball";
        public override string Description => "A ball of water that hits the target.";
        public int WetDuration => 4;
        public (int min, int max) BaseDamage => (2, 5);
        protected WaterballSkillTemplate(EffectTemplate[] effects) : base(effects) {

        }
        public override EffectTemplate[] GetSkillEffectTemplate() {
            return _effects.ToArray();
        }
        public override void Initialize() {
            //_damageEffect.Initialize(entity);
            foreach (var effect in _effects) {
                Console.WriteLine($"[WaterballSkillTemplate] Initializing effect {effect.EffectID}");
                effect.Initialize();
            }
        }
        public override Skill Instantiate(Entity owner, params object[] args) {
            var skill = new Skill(owner, this);
            return skill;
        }
        public override Effect[] GetEffects(Skill source, Entity target) {
            var effectInstances = new Effect[] {
                DamageEffectTemplate.Singleton.Instantiate(source, target, BaseDamage.min, BaseDamage.max),
                WetEffectTemplate.Singleton.Instantiate(source, target, WetDuration)
            };
            return effectInstances.ToArray();
        }

    }

    public class GuardSkillTemplate : SkillTemplate {
        static GuardSkillTemplate? _instance;
        public new static GuardSkillTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new GuardSkillTemplate(new EffectTemplate[] {
                        ProtectEffectTemplate.Singleton
                    });
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        public override string SkillID => "guard";
        public override string ClassName => typeof(GuardSkillTemplate).AssemblyQualifiedName ?? nameof(GuardSkillTemplate);
        public override string Label => "Guard";
        public override string Description => "Protects an ally by intercepting attacks directed at them.";
        public int ProtectDuration => 3; // Lasts 3 turns

        protected GuardSkillTemplate(EffectTemplate[] effects) : base(effects) { }

        static bool _initialized = false;
        public override void Initialize() {
            if (_initialized) {
                Console.WriteLine($"[GuardSkillTemplate] {SkillID} is already initialized.");
                return;
            }
            _initialized = true;
            Console.WriteLine($"[GuardSkillTemplate] Initializing {SkillID}.");

            // Initialize effect templates
            foreach (var effect in _effects) {
                Console.WriteLine($"[GuardSkillTemplate] Initializing effect {effect.EffectID}");
                effect.Initialize();
            }
        }

        public override Skill Instantiate(Entity owner, params object[] args) {
            var skill = new Skill(owner, this);
            return skill;
        }

        public override Effect[] GetEffects(Skill source, Entity target) {
            // Guard skill creates a protect effect where the skill user protects the target
            var effectInstances = new Effect[] {
                ProtectEffectTemplate.Singleton.Instantiate(source, target, ProtectDuration, source.Owner)
            };

            return effectInstances.ToArray();
        }

        public override EffectTemplate[] GetSkillEffectTemplate() {
            return _effects.ToArray();
        }
    }

    public class Skill {
        public string SkillID => SkillTemplate.SkillID;
        public Guid ID { get; } = Guid.NewGuid();
        public string Label => SkillTemplate.Label;
        public string Description => SkillTemplate.Description;
        public Entity Owner { get; }
        public SkillTemplate SkillTemplate { get; }
        public virtual float Accuracy => 1f; // 100% chance to hit

        public Skill(Entity owner, SkillTemplate template) {
            Owner = owner;
            SkillTemplate = template;
        }
        public void Use(Entity target) {
            Console.WriteLine($"[Skill.Use]: {Owner.Name} uses skill {Label} on {target.Name}.");
            var effects = SkillTemplate.GetEffects(this, target);
            foreach (var effect in effects) {
                target.ApplyEffect(effect);
            }
        }

        public void UseOnAlly(Entity ally) {
            Console.WriteLine($"[Skill.UseOnAlly]: {Owner.Name} uses {Label} to protect {ally.Name}.");
            var effects = SkillTemplate.GetEffects(this, ally);
            foreach (var effect in effects) {
                ally.ApplyEffect(effect);
            }
        }
    }

    #endregion

    #region Effect System
    public abstract class EffectTemplate {
        public static EffectTemplate Singleton => throw new LSNotImplementedException("Singleton not implemented for EffectTemplate");
        public abstract string EffectID { get; }
        public abstract string ClassName { get; }
        protected EffectTemplate() { }

        public abstract void Initialize();
        public abstract void Cleanup();
        public abstract Effect Instantiate(Skill source, Entity target, params object[] args);
        public abstract void Apply(Effect sourceEffect);
        public abstract void Remove(Effect sourceEffect);
    }
    /// <summary>
    /// A template for a damage effect that can be applied to an entity.
    /// DamageEffectTemplate Event nodes:
    /// - OnEffectAppliedEvent: Handles the application of damage when the effect is applied.
    /// -- Sequence "damageEffectTemplate": Main sequence for processing the damage effect.
    /// --- Selector "rollDamage": Rolls the damage amount based on the source stats or other modifiers.
    /// ---- Event Data: [rollDamage] is an int representing the rolled damage amount.
    /// --- Selector "damageModifier": Applies any damage modifiers (like strength or buffs).
    /// ---- Event Data: [damageModifiers] is a list of int representing all damage modifiers to be applied.
    /// --- Selector "mitigateDamage": Applies any damage mitigation effects (like armor or buffs).
    /// ---- Event Data: [targetArmor] is an int representing the target's armor value.
    /// --- Selector "applyDamage": Calculates the final damage and applies it to the target's health.
    /// </summary>
    public class DamageEffectTemplate : EffectTemplate {
        static DamageEffectTemplate? _instance = null;
        public static new DamageEffectTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new DamageEffectTemplate();
                    _instance.Initialize();
                }
                return _instance;
            }
        }
        protected Dictionary<Effect, (int min, int max)> _effectDamageValues = new();
        public override string EffectID => "damageEffect";
        public override string ClassName => typeof(DamageEffectTemplate).AssemblyQualifiedName ?? nameof(DamageEffectTemplate);
        protected DamageEffectTemplate() { }
        bool _isInitialized = false;
        public override void Initialize() {
            if (_isInitialized) {
                Console.WriteLine($"[DamageEffectTemplate] {ClassName} is already initialized.");
                return;
            }
            _isInitialized = true;
            Console.WriteLine($"[DamageEffectTemplate] Initializing {ClassName}.");
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
               .Sequence($"{ClassName}", main => main
                   // damage roll must be high priority to ensure it runs before apply damage
                   .Selector($"rollDamage", selRoll => selRoll
                       .Execute("successRollDamage", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                   , LSPriority.HIGH // rollDamage should be high priority so that it runs first
                   )
                   .Selector("damageModifier", selMod => selMod
                       .Execute("successDamageModifier", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                   , LSPriority.NORMAL // damageModifier should run after rollDamage but before mitigateDamage and applyDamage
                   )
                   .Selector("applyDamage", sel => sel // apply the damage 
                       .Execute("successApplyDamage", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                   , LSPriority.BACKGROUND) // applyDamage should be the last selector to run in the sequence
               , LSPriority.NORMAL, // damageEffectTemplate should be normal priority so that it runs after any logging or other critical handlers
                   withInverter: false,
                   overrideConditions: false,
                   conditions: (evt, node) => {
                       if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for damage effect.");
                       // Console.WriteLine($"[DamageEffectTemplate] {instance.Name} checking conditions for applying damage effect to {appliedEffect.Target.Name}.");
                       if (appliedEffect.Effect.EffectTemplate is not DamageEffectTemplate) return false; // only apply if the effect is a damage effect
                       return true;
                   }
               )
            );
        }
        public override void Cleanup() {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{ClassName}"));
        }

        public override Effect Instantiate(Skill source, Entity target, params object[] args) {
            if (args.Length != 2 || args[0] is not int minDamage || args[1] is not int maxDamage) {
                throw new LSException("DamageEffectTemplate requires two integer parameters: minDamage and maxDamage.");
            }

            Effect effect = new Effect(source, target, this);
            _effectDamageValues[effect] = (minDamage, maxDamage);
            return effect;
        }
        public bool TryGetDamageRange(Effect effect, out (int min, int max) damageRange) {
            if (!_effectDamageValues.TryGetValue(effect, out damageRange)) {
                return false;
            }
            return true;
        }

        public override void Apply(Effect sourceEffect) {

            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence($"{ClassName}", main => main
                    .Selector($"rollDamage", selRoll => selRoll
                        .Execute($"{sourceEffect.ID}", (evt, ctx) => { //calculate how much damage the effect is going to deal
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            var source = appliedEffect.Source;
                            // check if the event already has rollDamage data (from buffs/debuffs), if not, roll it now
                            // if (!evt.TryGetData<int>("rollDamageModifier", out var rollDamageModifier)) {
                            //     rollDamageModifier = 0;
                            // }
                            if (!_effectDamageValues.TryGetValue(sourceEffect, out var damageAmount)) {
                                throw new LSException($"[DamageEffectTemplate]: Damage values not found for effect instance {sourceEffect.ID}.");
                            }
                            int rollDamage = new Random().Next(damageAmount.min, damageAmount.max + 1);
                            Console.WriteLine($"[DamageEffectTemplate] Effect {sourceEffect.ID}: Rolled damage for {appliedEffect.Source.Label} is {rollDamage}.");
                            // store the rolled values in the event data for other nodes to use
                            evt.SetData("rollDamage", rollDamage);

                            return LSEventProcessStatus.SUCCESS; //we return success, but the node will actually fail because of inverter
                        }, LSPriority.LOW // this should be the last non-success handler in rollDamage to run, this make sure that any other buffs/debuffs that modify rollDamage or rollStrength run first
                        , true) // we invert the result so that other lower priority handlers can also run, handler is inverted
                    )
                    .Selector("damageModifier", selMod => selMod
                        .Execute($"{sourceEffect.ID}", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            // Here you can add logic to modify the damage based on buffs/debuffs
                            var source = appliedEffect.Source;
                            if (!evt.TryGetData<List<int>>("damageModifiers", out var damageModifiers)) {
                                damageModifiers = new List<int>();
                                Console.WriteLine($"[DamageEffectTemplate] Caller {sourceEffect.ID}: Initializing damageModifiers list.");
                            }
                            // for now we don't add strength, it actually should be added by the "strength stat", we just set the list so that others can add modifiers.
                            // int strengthModifier = new Random().Next(source.Strength.min, source.Strength.max + 1);
                            // damageModifiers.Add(strengthModifier);
                            evt.SetData("damageModifiers", damageModifiers);
                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.CRITICAL) // we invert the result so that other lower priority handlers can also run, handler is inverted, also since this will set the list, we run at a critical priority so that it runs before any other modifier
                    )
                    .Selector("applyDamage", sel => sel // apply the damage 
                        .Execute($"{sourceEffect.ID}", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            int currentDamage = 0;
                            if (!evt.TryGetData<int>("rollDamage", out var rollDamage)) {
                                Console.WriteLine($"[DamageEffectTemplate] Caller {sourceEffect.ID}: rollDamage data not found in event.");
                                rollDamage = 0;
                            }
                            currentDamage += rollDamage;
                            if (!evt.TryGetData<List<int>>("damageModifiers", out var damageModifiers)) {
                                damageModifiers = new List<int>();
                            }
                            foreach (var mod in damageModifiers) currentDamage += mod;

                            // Check for damage redirection
                            Entity actualTarget = appliedEffect.Target;
                            if (evt.TryGetData<Entity>("redirectedTo", out var redirectedTarget)) {
                                actualTarget = redirectedTarget;
                                Console.WriteLine($"[DamageEffectTemplate] Damage redirected from {appliedEffect.Target.Name} to {actualTarget.Name}");
                            }

                            actualTarget.Health -= currentDamage;
                            Console.WriteLine($"[DamageEffectTemplate] Caller {sourceEffect.ID}: {appliedEffect.Source.Label} deals [{currentDamage}] damage to [{actualTarget.Name}]. [{actualTarget.Name}] has [{actualTarget.Health}] health remaining.");
                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.LOW, true // we invert the result so that if applyDamage fails, the whole sequence does not fail (which would prevent other effects from being applied
                        )
                    , LSPriority.BACKGROUND) // applyDamage should be the last selector to run in the sequence
                                             // probably we don't need to add a condition in this case, since the sequence itself in the global context should be already conditioned to only run for damage effects
                                             // , LSPriority.NORMAL, // damageEffectTemplate should be normal priority so that it runs after any logging or other critical handlers
                                             //     withInverter: false,
                                             //     overrideConditions: false,
                                             //     conditions: (evt, node) => {
                                             //         if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for damage effect.");
                                             //         // Console.WriteLine($"[DamageEffectTemplate] {instance.Name} checking conditions for applying damage effect to {appliedEffect.Target.Name}.");
                                             //         if (appliedEffect.Effect.EffectTemplate is not DamageEffectTemplate) return false; // only apply if the effect is a damage effect
                                             //         return true;
                                             //     }
                ), sourceEffect.Target
                );

        }
        public override void Remove(Effect sourceEffect) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{ClassName}"), sourceEffect.Target);
        }
    }

    public class WetEffectTemplate : EffectTemplate {
        static WetEffectTemplate? _instance = null;
        public static new WetEffectTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new WetEffectTemplate();
                    _instance.Initialize();
                }
                return _instance;
            }
        }
        public override string EffectID => "wetEffect";
        public override string ClassName => typeof(WetEffectTemplate).AssemblyQualifiedName ?? nameof(WetEffectTemplate);
        Dictionary<Effect, (int duration, bool isApplied)> _effectStatus = new();

        public WetEffectTemplate() {
        }
        bool _isInitialized = false;
        public override void Initialize() {
            if (_isInitialized) {
                Console.WriteLine($"[WetEffectTemplate] {ClassName} is already initialized.");
                return;
            }
            _isInitialized = true;

            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence(ClassName, mainBuilder => mainBuilder
                    .Selector("applyEffect", wetEffectBuilder => wetEffectBuilder
                        .Execute("successWetEffect", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND, true)
                    , LSPriority.CRITICAL,
                        withInverter: false,
                        overrideConditions: true,
                        conditions: (evt, node) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for wet effect.");
                            if (!_effectStatus.TryGetValue(appliedEffect.Effect, out (int duration, bool isApplied) status)) {
                                throw new LSException($"[WetEffectTemplate]: Status not found for effect instance {appliedEffect.Effect.ID}.");
                            }
                            if (status.isApplied) return false; // only apply if the effect is not already applied

                            return true;
                        }
                    )
                    .Selector("updateEffect", updateEffectBuilder => updateEffectBuilder
                        .Execute("successUpdateWetEffect", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND, true)
                    )
                ));
        }
        public override void Cleanup() {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild(ClassName));
        }
        public override Effect Instantiate(Skill source, Entity target, params object[] args) {
            if (args == null || args[0] is not int duration) {
                throw new LSException("WetEffectTemplate requires one integer parameter: duration.");
            }
            Effect effect = new Effect(source, target, this);
            _effectStatus[effect] = (duration, false);
            return effect;
        }
        public override void Apply(Effect sourceEffect) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence(ClassName, mainBuilder => mainBuilder
                    .Selector("applyEffect", applyEffectBuilder => applyEffectBuilder
                        .Execute($"{sourceEffect.ID}", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            if (!_effectStatus.TryGetValue(sourceEffect, out (int duration, bool isApplied) status)) {
                                throw new LSException($"[WetEffectTemplate]: Status not found for effect instance {sourceEffect.ID}.");
                            }
                            status.isApplied = true;
                            _effectStatus[sourceEffect] = status;
                            return LSEventProcessStatus.SUCCESS;
                        }, priority: LSPriority.LOW, withInverter: true, conditions: (evt, node) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for wet effect.");
                            if (appliedEffect.Effect.EffectTemplate is not WetEffectTemplate) return false; // only apply if the effect is a wet effect
                            return true;
                        })
                    )
                    .Selector("updateEffect", updEffect => updEffect
                        .Execute($"{sourceEffect.ID}",
                            (evt, ctx) => {
                                if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                                if (!_effectStatus.TryGetValue(sourceEffect, out (int duration, bool isApplied) status)) {
                                    throw new LSException($"[WetEffectTemplate]: Status not found for effect instance {sourceEffect.ID}.");
                                }
                                if (!status.isApplied) return LSEventProcessStatus.SUCCESS; // only update if the effect is applied
                                status.duration -= 1;
                                if (status.duration <= 0) {
                                    status.isApplied = false;
                                    Console.WriteLine($"[WetEffectTemplate] Effect {sourceEffect.ID} on {appliedEffect.Target.Name} has expired.");
                                } else {
                                    Console.WriteLine($"[WetEffectTemplate] Effect {sourceEffect.ID} on {appliedEffect.Target.Name} has {status.duration} turns remaining.");
                                }
                                _effectStatus[sourceEffect] = status;
                                return LSEventProcessStatus.SUCCESS;
                            }, priority: LSPriority.LOW, withInverter: true,
                            conditions: (evt, node) => {
                                if (evt is not OnEffectAppliedEvent appliedEffect) throw new LSException("Invalid event type for wet effect.");
                                if (appliedEffect.Effect.EffectTemplate is not WetEffectTemplate) return false; // only apply if the effect is a wet effect
                                return true;
                            }
                        )
                     )
                ), sourceEffect.Target);
        }
        public override void Remove(Effect sourceEffect) {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence(ClassName, main => main
                    .RemoveChild("wetWatcher")
                ));
        }
    }

    /// <summary>
    /// A template for a protect effect that can intercept damage to protected targets.
    /// ProtectEffectTemplate Event nodes:
    /// - OnEffectAppliedEvent: Handles the application of protect status when the effect is applied.
    /// -- Sequence "protectEffectTemplate": Main sequence for processing the protect effect.
    /// --- Selector "applyProtect": Applies the protect status to the target.
    /// ---- Event Data: [protectDuration] is an int representing how many turns the protection lasts.
    /// --- Selector "updateProtect": Updates the protect duration each turn.
    /// - OnHitEvent: Intercepts damage to protected targets and redirects to protector.
    /// -- Selector "interceptDamage": Checks if target is protected and redirects damage.
    /// ---- Event Data: [originalDamage] is preserved, [redirectedTo] indicates the protector.
    /// </summary>
    public class ProtectEffectTemplate : EffectTemplate {
        static ProtectEffectTemplate? _instance = null;
        public static new ProtectEffectTemplate Singleton {
            get {
                if (_instance == null) {
                    _instance = new ProtectEffectTemplate();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        protected Dictionary<Effect, (int duration, bool isActive, Entity protector)> _protectStatus = new();
        protected Dictionary<Entity, List<Effect>> _activeEffects = new(); // Track effects per entity
        public override string EffectID => "protectEffect";
        public override string ClassName => typeof(ProtectEffectTemplate).AssemblyQualifiedName ?? nameof(ProtectEffectTemplate);

        protected ProtectEffectTemplate() { }
        bool _isInitialized = false;

        public override void Initialize() {
            if (_isInitialized) {
                Console.WriteLine($"[ProtectEffectTemplate] {ClassName} is already initialized.");
                return;
            }
            _isInitialized = true;
            Console.WriteLine($"[ProtectEffectTemplate] Initializing {ClassName}.");

            // Global handler for OnEffectAppliedEvent - manages protect status
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence($"{ClassName}", main => main
                    .Selector("applyProtect", selApply => selApply
                        .Execute("successApplyProtect", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent appliedEffect) return LSEventProcessStatus.CANCELLED;
                            if (!_protectStatus.TryGetValue(appliedEffect.Effect, out var status)) {
                                Console.WriteLine($"[ProtectEffectTemplate] Status not found for effect {appliedEffect.Effect.ID}");
                                return LSEventProcessStatus.FAILURE;
                            }
                            Console.WriteLine($"[ProtectEffectTemplate] {status.protector.Name} is now protecting {appliedEffect.Target.Name} for {status.duration} turns.");
                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.HIGH)
                    , LSPriority.HIGH)
                    .Selector("updateProtect", selUpdate => selUpdate
                        .Execute("successUpdateProtect", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)
                    , LSPriority.NORMAL)
                , LSPriority.NORMAL,
                    withInverter: false,
                    overrideConditions: false,
                    conditions: (evt, node) => {
                        if (evt is not OnEffectAppliedEvent appliedEffect) return false;
                        return appliedEffect.Effect.EffectTemplate is ProtectEffectTemplate;
                    }
                )
            );

            // Global handler for OnEffectUpdateEvent - manages effect duration
            LSEventContextManager.Singleton.Register<OnEffectUpdateEvent>(root => root
                .Sequence("protectUpdate", main => main
                    .Selector("updateProtection", selUpdate => selUpdate
                        .Execute("updateDuration", (evt, ctx) => {
                            if (evt is not OnEffectUpdateEvent updateEvent) return LSEventProcessStatus.CANCELLED;

                            // Check if this entity has active protect effects
                            if (!_activeEffects.TryGetValue(updateEvent.Target, out var effects)) {
                                return LSEventProcessStatus.SUCCESS;
                            }

                            // Update all protect effects for this entity
                            var effectsToRemove = new List<Effect>();
                            foreach (var effect in effects) {
                                if (!_protectStatus.TryGetValue(effect, out var status)) continue;

                                status.duration -= 1;
                                if (status.duration <= 0) {
                                    status.isActive = false;
                                    effectsToRemove.Add(effect);
                                    Console.WriteLine($"[ProtectEffectTemplate] Protection from {status.protector.Name} on {updateEvent.Target.Name} has expired.");
                                } else {
                                    Console.WriteLine($"[ProtectEffectTemplate] {status.protector.Name}'s protection on {updateEvent.Target.Name} has {status.duration} turns remaining.");
                                }
                                _protectStatus[effect] = status;
                            }

                            // Remove expired effects
                            foreach (var expiredEffect in effectsToRemove) {
                                effects.Remove(expiredEffect);
                                _protectStatus.Remove(expiredEffect);
                                expiredEffect.EffectTemplate.Remove(expiredEffect);
                            }

                            // Clean up empty lists
                            if (effects.Count == 0) {
                                _activeEffects.Remove(updateEvent.Target);
                            }

                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.NORMAL)
                    , LSPriority.NORMAL)
                , LSPriority.NORMAL)
            );

            // Global handler for OnEffectAppliedEvent - intercepts damage effects to protected targets
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .Sequence("protectDamageInterception", main => main
                    .Selector("checkDamageProtection", selCheck => selCheck
                        .Execute("interceptDamageEffect", (evt, ctx) => {
                            if (evt is not OnEffectAppliedEvent effectEvent) return LSEventProcessStatus.CANCELLED;

                            // Only intercept damage effects
                            if (effectEvent.Effect.EffectTemplate is not DamageEffectTemplate) {
                                return LSEventProcessStatus.SUCCESS;
                            }

                            // Check if target has active protect effects
                            if (!_activeEffects.TryGetValue(effectEvent.Target, out var effects)) {
                                return LSEventProcessStatus.SUCCESS;
                            }

                            // Find first active protector
                            foreach (var effect in effects) {
                                if (_protectStatus.TryGetValue(effect, out var status) && status.isActive && status.duration > 0) {
                                    Console.WriteLine($"[ProtectEffectTemplate] {status.protector.Name} intercepts damage effect on {effectEvent.Target.Name}!");

                                    // Store redirection info in the event data
                                    evt.SetData("originalTarget", effectEvent.Target);
                                    evt.SetData("redirectedTo", status.protector);
                                    evt.SetData("wasRedirected", true);

                                    return LSEventProcessStatus.SUCCESS;
                                }
                            }

                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.CRITICAL) // Very high priority to intercept before damage calculation
                    , LSPriority.CRITICAL)
                , LSPriority.CRITICAL,
                    withInverter: false,
                    overrideConditions: false,
                    conditions: (evt, node) => {
                        if (evt is not OnEffectAppliedEvent effectEvent) return false;
                        return effectEvent.Effect.EffectTemplate is DamageEffectTemplate;
                    }
                )
            );
        }

        public override void Cleanup() {
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild($"{ClassName}"));
            LSEventContextManager.Singleton.Register<OnEffectUpdateEvent>(root => root
                .RemoveChild("protectUpdate"));
            LSEventContextManager.Singleton.Register<OnEffectAppliedEvent>(root => root
                .RemoveChild("protectDamageInterception"));
        }

        public override Effect Instantiate(Skill source, Entity target, params object[] args) {
            if (args.Length != 2 || args[0] is not int duration || args[1] is not Entity protector) {
                throw new LSException("ProtectEffectTemplate requires two parameters: duration (int) and protector (Entity).");
            }

            Effect effect = new Effect(source, target, this);
            _protectStatus[effect] = (duration, true, protector);

            // Register the effect with the target entity
            if (!_activeEffects.ContainsKey(target)) {
                _activeEffects[target] = new List<Effect>();
            }
            _activeEffects[target].Add(effect);

            return effect;
        }

        public bool TryGetProtectStatus(Effect effect, out (int duration, bool isActive, Entity protector) status) {
            return _protectStatus.TryGetValue(effect, out status);
        }

        public bool HasActiveEffect(Entity entity) {
            return _activeEffects.ContainsKey(entity) && _activeEffects[entity].Any(effect =>
                _protectStatus.TryGetValue(effect, out var status) && status.isActive && status.duration > 0);
        }

        public override void Apply(Effect sourceEffect) {
            // Apply method is called once when the effect is first applied
            // The effect now manages itself through the global event handlers
            if (_protectStatus.TryGetValue(sourceEffect, out var status)) {
                Console.WriteLine($"[ProtectEffectTemplate] Activating protection: {status.protector.Name} protects {sourceEffect.Target.Name}");
            }
        }

        public override void Remove(Effect sourceEffect) {
            if (_protectStatus.TryGetValue(sourceEffect, out var status)) {
                Console.WriteLine($"[ProtectEffectTemplate] Removing protection from {status.protector.Name} on {sourceEffect.Target.Name}");
                _protectStatus.Remove(sourceEffect);

                // Remove from active effects
                if (_activeEffects.TryGetValue(sourceEffect.Target, out var effects)) {
                    effects.Remove(sourceEffect);
                    if (effects.Count == 0) {
                        _activeEffects.Remove(sourceEffect.Target);
                    }
                }
            }
        }
    }
    /**/
    #region Effect Instance
    public class Effect {
        public Guid ID { get; }
        public string EffectID => EffectTemplate.EffectID;
        public Skill Source { get; }
        public Entity Target { get; }
        public EffectTemplate EffectTemplate { get; }
        public Effect(Skill source, Entity target, EffectTemplate effectTemplate) {
            ID = Guid.NewGuid();
            Source = source;
            Target = target;
            EffectTemplate = effectTemplate;
        }
        public void Apply() {
            EffectTemplate.Apply(this);
        }
        public void Remove() {
            EffectTemplate.Remove(this);
        }
    }
    public class OnEffectAppliedEvent : LSEvent {
        public Skill Source { get; }
        public Entity Target { get; }
        public Effect Effect { get; }
        public OnEffectAppliedEvent(Effect effect) {
            Target = effect.Target;
            Source = effect.Source;
            Effect = effect;
        }
    }

    public class OnEffectUpdateEvent : LSEvent {
        public Entity Target { get; }
        public OnEffectUpdateEvent(Entity target) {
            Target = target;
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
        public void Initialize() {
            if (_isInitialized) {
                throw new LSException($"TargetMechanic for {_owner.Name} is already initialized.");
            }
            _isInitialized = true;
            string instance = _owner.ID.ToString();
            //Console.WriteLine($"Initializing TargetMechanic for {_owner.Name}");
            // OnHitEvent for owner
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Sequence($"targetMechanic", selMechanic => selMechanic
                    .Selector("hitTarget", sel => sel
                        .Execute($"{instance}_skillUse", (evt, ctx) => {
                            if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                            Console.WriteLine($"[OnHitEvent] Instance {instance}: {onHitEvent.Source.Name} use skill {onHitEvent.Skill?.Label} on {onHitEvent.Target.Name}.");
                            onHitEvent.Skill?.Use(onHitEvent.Target);
                            return LSEventProcessStatus.SUCCESS;
                        }, LSPriority.LOW) //the use skill node should be the last to run, this gives the change of other entities to react to the hit first (like protect)
                    )
                , LSPriority.BACKGROUND, withInverter: false, overrideConditions: false), _owner);

            // any entity that is not the source or target of the hit event can watch the event and react to it
            LSEventContextManager.Singleton.Register<OnHitEvent>(root => root
                .Sequence($"targetMechanic", selMechanic => selMechanic
                    .Selector($"{_owner.ID}_watcher", selWatcher => selWatcher // all entities watch the event, but only those that are not source or target will be able actually "watch" the onHitEvent
                        .Execute($"{_owner.ID}", (evt, ctx) => {
                            if (evt is not OnHitEvent onHitEvent) return LSEventProcessStatus.CANCELLED;
                            //if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return LSEventProcessStatus.SUCCESS;
                            Console.WriteLine($"[OnHitEvent] {_owner.Name} sees {onHitEvent.Source.Name} hitting {onHitEvent.Target.Name} and did nothing.");
                            return LSEventProcessStatus.SUCCESS; // we return success so that other watchers can also run, handler is inverted
                        }, LSPriority.LOW, true) //we give this handler a low priority so that it runs after other handlers (like protect)
                        .Execute("successOnHitWatcher", (evt, ctx) => LSEventProcessStatus.SUCCESS, LSPriority.BACKGROUND)

                    , LSPriority.HIGH, //onHitWatcher should run before hitTarget
                        withInverter: false,
                        overrideConditions: false,
                        conditions: (evt, node) => {
                            if (evt is not OnHitEvent onHitEvent) throw new LSException("Invalid event type for target selector.");
                            //Console.WriteLine($"[OnHitEvent] {_owner.Name} checking conditions for watching hit event between {onHitEvent.Source.Name} and {onHitEvent.Target.Name}.");
                            if (_owner == onHitEvent.Source || _owner == onHitEvent.Target) return false;
                            return true;
                        })
                    )
                );
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
        //var protectTemplate = new ProtectEffectTemplate(2);

        entityA.Initialize(null!, null!);
        entityB.Initialize(null!, null!);
        entityC.Initialize(null!, null!);
        entityD.Initialize(null!, null!);

        var punchA = PunchSkillTemplate.Singleton.Instantiate(entityA);
        var punchB = PunchSkillTemplate.Singleton.Instantiate(entityB);
        var punchD = PunchSkillTemplate.Singleton.Instantiate(entityD);
        var waterBallC = WaterballSkillTemplate.Singleton.Instantiate(entityC);

        entityA.EquippedSkill = punchA;
        entityB.EquippedSkill = punchB;
        entityC.EquippedSkill = waterBallC;
        entityD.EquippedSkill = punchD;


        Assert.That(entityA.EquippedSkill != null);
        Assert.That(entityB.EquippedSkill != null);
        Assert.That(entityC.EquippedSkill != null);
        Assert.That(entityD.EquippedSkill != null);

        Console.WriteLine("---- Combat Start ----");
        //entityA.Hit(entityB);
        entityC.Hit(entityD);
        // entityA.Hit(entityB);
        // entityA.Hit(entityB);
        // entityA.Hit(entityB);
        //entityC.Hit(entityD);
    }

    [Test]
    public void TestGuardProtectEffect() {
        Console.WriteLine("=== Testing Guard/Protect Effect ===");

        // Create entities
        var guardian = new Entity("Guardian");
        var protectedEntity = new Entity("Protected");
        var attacker = new Entity("Attacker");

        // Initialize entities
        guardian.Initialize(null!, null!);
        protectedEntity.Initialize(null!, null!);
        attacker.Initialize(null!, null!);

        // Create skills
        var guardSkill = GuardSkillTemplate.Singleton.Instantiate(guardian);
        var punchSkill = PunchSkillTemplate.Singleton.Instantiate(attacker);

        guardian.EquippedSkill = guardSkill;
        attacker.EquippedSkill = punchSkill;

        // Record initial health values
        var guardianInitialHealth = guardian.Health;
        var protectedInitialHealth = protectedEntity.Health;

        Console.WriteLine($"Initial Health - Guardian: {guardianInitialHealth}, Protected: {protectedInitialHealth}");

        // Guardian uses guard skill to protect the protected entity
        Console.WriteLine("\n--- Guardian casts Guard on Protected ---");
        guardSkill.UseOnAlly(protectedEntity);

        // Verify protect effect was applied by checking the template's internal registry
        var hasProtectEffect = ProtectEffectTemplate.Singleton.HasActiveEffect(protectedEntity);
        Assert.That(hasProtectEffect, Is.True, "Protected entity should have an active protect effect");

        Console.WriteLine($"Guard effect applied successfully on {protectedEntity.Name}.");

        // Attacker hits the protected entity
        Console.WriteLine("\n--- Attacker attacks Protected (should be intercepted by Guardian) ---");
        attacker.Hit(protectedEntity);

        // Check if damage was redirected to guardian
        Console.WriteLine($"After attack - Guardian Health: {guardian.Health}, Protected Health: {protectedEntity.Health}");

        // The protected entity should not take damage (guardian should intercept)
        // Guardian should take damage instead
        Assert.That(protectedEntity.Health, Is.EqualTo(protectedInitialHealth), "Protected entity should not take damage");
        Assert.That(guardian.Health, Is.LessThan(guardianInitialHealth), "Guardian should take damage from interception");

        Console.WriteLine("=== Guard/Protect Test Completed Successfully ===");
    }

    [Test]
    public void TestGuardProtectEffectDuration() {
        Console.WriteLine("=== Testing Guard/Protect Effect Duration ===");

        // Create entities
        var guardian = new Entity("Guardian");
        var protectedEntity = new Entity("Protected");
        var attacker = new Entity("Attacker");

        // Initialize entities
        guardian.Initialize(null!, null!);
        protectedEntity.Initialize(null!, null!);
        attacker.Initialize(null!, null!);

        // Create skills
        var guardSkill = GuardSkillTemplate.Singleton.Instantiate(guardian);
        var punchSkill = PunchSkillTemplate.Singleton.Instantiate(attacker);

        guardian.EquippedSkill = guardSkill;
        attacker.EquippedSkill = punchSkill;

        // Guardian uses guard skill to protect the protected entity
        Console.WriteLine("\n--- Guardian casts Guard on Protected ---");
        guardSkill.UseOnAlly(protectedEntity);

        // Test multiple attack rounds to verify duration mechanics
        for (int turn = 1; turn <= 4; turn++) {
            Console.WriteLine($"\n--- Turn {turn} ---");

            var guardianHealth = guardian.Health;
            var protectedHealth = protectedEntity.Health;

            // Check if protection is still active
            var hasProtectEffect = ProtectEffectTemplate.Singleton.HasActiveEffect(protectedEntity);
            Console.WriteLine($"Protection active: {hasProtectEffect}");

            if (hasProtectEffect) {
                // Attack should be redirected to guardian
                Console.WriteLine($"Attacker attacks Protected (should be intercepted by Guardian)");
                attacker.Hit(protectedEntity);

                Assert.That(protectedEntity.Health, Is.EqualTo(protectedHealth), $"Turn {turn}: Protected entity should not take damage");
                Assert.That(guardian.Health, Is.LessThan(guardianHealth), $"Turn {turn}: Guardian should take damage from interception");
            } else {
                // Protection has expired, attack hits protected entity
                Console.WriteLine($"Attacker attacks Protected (protection expired)");
                attacker.Hit(protectedEntity);

                Assert.That(protectedEntity.Health, Is.LessThan(protectedHealth), $"Turn {turn}: Protected entity should take damage when protection expires");
                Assert.That(guardian.Health, Is.EqualTo(guardianHealth), $"Turn {turn}: Guardian should not take damage when protection expires");
            }

            // Simulate turn passing (trigger effect updates)
            protectedEntity.Update();

            Console.WriteLine($"Guardian Health: {guardian.Health}, Protected Health: {protectedEntity.Health}");
        }

        Console.WriteLine("=== Guard/Protect Duration Test Completed Successfully ===");
    }
}
public static class EffectExtensions {
    public static (int min, int max) GetDamage(this AEffectSystemEvent_tests.Effect effect) {
        if (!AEffectSystemEvent_tests.DamageEffectTemplate.Singleton.TryGetDamageRange(effect, out var damageRange)) {
            throw new LSException($"Damage values not found for effect instance {effect.ID}.");
        }
        return damageRange;
    }
}
public static class Vector2Extensions {
    public static float DistanceTo(this Vector2 a, Vector2 b) {
        return Vector2.Distance(a, b);
    }
}
