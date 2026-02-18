namespace LSUtils.Tests.ECS;

using System.Linq;
using LSUtils.ECS;
using NUnit.Framework;

[TestFixture]
public class WorldTest {

    #region Mock Classes

    public class TestComponent : IComponent {
        public string ComponentName => "TestComponent";
        public int Value { get; set; }

    }
    public class AnotherTestComponent : IComponent {
        public string ComponentName => "AnotherTestComponent";
        public int Value { get; set; }

    }
    public class ThirdTestComponent : IComponent {
        public string ComponentName => "ThirdTestComponent";
        public int Value { get; set; }

    }
    public class TestSystem : ISystem {
        public string SystemName => "TestSystem";
        IWorld? _world;
        public void Initialize(IWorld world) {
            _world = world;
            _world.RegisterSystem(this);
        }
        public void Update(float deltaTime) {

        }
        public void Shutdown() {
            if (_world == null) throw new LSNullReferenceException("World reference is null.");
            _world.UnregisterSystem<TestSystem>();
        }
    }
    public class CountingSystem : ISystem {
        public string SystemName => "CountingSystem";
        public int UpdateCount { get; private set; }
        IWorld? _world;

        public void Initialize(IWorld world) {
            _world = world;
            _world.RegisterSystem(this);
        }

        public void Update(float deltaTime) {
            UpdateCount++;
        }

        public void Shutdown() {
            if (_world == null) throw new LSNullReferenceException("World reference is null.");
            _world.UnregisterSystem<CountingSystem>();
        }
    }

    #endregion

    private LSWorld _world;

    [SetUp]
    public void Setup() {
        _world = new LSWorld();
    }

    [Test]
    public void CreateEntity_ShouldAddEntityToWorld() {
        var entity = _world.CreateEntity<LSEntity>();
        Assert.That(_world.GetEntity(entity.ID), Is.Not.Null);
    }

    [Test]
    public void CreateEntity_WithIdAndName_ShouldUseProvidedValues() {
        var id = System.Guid.NewGuid();
        var name = "Test Entity";

        var entity = _world.CreateEntity<LSEntity>(id, name);

        Assert.That(entity.ID, Is.EqualTo(id));
        Assert.That(entity.Name, Is.EqualTo(name));
        Assert.That(_world.GetEntity(id), Is.SameAs(entity));
    }

    [Test]
    public void DestroyEntity_ShouldRemoveEntityFromWorld() {
        var entity = _world.CreateEntity<LSEntity>();
        var result = _world.DestroyEntity(entity.ID);
        Assert.That(result, Is.True);
        Assert.Throws<LSNullReferenceException>(() => _world.GetEntity(entity.ID));
    }
    [Test]
    public void RegisterSystem_ShouldAddSystemToWorld() {
        var system = new TestSystem();
        system.Initialize(_world);
        var retrievedSystem = _world.GetSystem<TestSystem>();
        Assert.That(retrievedSystem, Is.Not.Null);
        Assert.That(retrievedSystem.SystemName, Is.EqualTo(system.SystemName));
    }
    [Test]
    public void UnregisterSystem_ShouldRemoveSystemFromWorld() {
        var system = new TestSystem();
        system.Initialize(_world);
        Assert.That(_world.GetSystem<TestSystem>(), Is.Not.Null);
        system.Shutdown();
        Assert.Throws<LSNullReferenceException>(() => _world.GetSystem<TestSystem>());
    }

    [Test]
    public void GetEntitiesWith_ShouldReturnCorrectEntities() {
        var entity1 = _world.CreateEntity<LSEntity>();
        var entity2 = _world.CreateEntity<LSEntity>();
        var component1 = new TestComponent { Value = 10 };
        var component2 = new TestComponent { Value = 20 };
        entity1.AddComponent(component1);
        entity2.AddComponent(component2);

        var entitiesWithComponent1 = _world.GetEntitiesWith<TestComponent>(out var components).ToList();
        Assert.That(entitiesWithComponent1.Count, Is.EqualTo(2));
        Assert.That(components.Count(), Is.EqualTo(2));
        Assert.That(components.Any(c => c != null && c.Value == 10), Is.True);
        Assert.That(components.Any(c => c != null && c.Value == 20), Is.True);
    }
    [Test]
    public void GetEntitiesWith_ShouldReturnEmptyWhenNoEntitiesHaveComponent() {
        var entities = _world.GetEntitiesWith<TestComponent>(out var components).ToList();
        Assert.That(entities.Count, Is.EqualTo(0));
        Assert.That(components.Count(), Is.EqualTo(0));
    }
    [Test]
    public void GetEntitiesWith_ShouldReturnOnlyEntitiesWithAllComponents() {
        var entity1 = _world.CreateEntity<LSEntity>();
        var entity2 = _world.CreateEntity<LSEntity>();
        var component1 = new TestComponent { Value = 10 };
        var component2 = new AnotherTestComponent { Value = 20 };
        entity1.AddComponent(component1);
        entity1.AddComponent(component2);
        entity2.AddComponent(component1);

        var entitiesWithBothComponents = _world.GetEntitiesWith<TestComponent, AnotherTestComponent>().ToList();
        Assert.That(entitiesWithBothComponents.Count, Is.EqualTo(1));
        Assert.That(entitiesWithBothComponents[0].ID, Is.EqualTo(entity1.ID));
    }
    [Test]
    public void GetEntitiesWith_ShouldReturnEmptyWhenNoEntitiesHaveAllComponents() {
        var entity1 = _world.CreateEntity<LSEntity>();
        var entity2 = _world.CreateEntity<LSEntity>();
        var component1 = new TestComponent { Value = 10 };
        var component2 = new AnotherTestComponent { Value = 20 };
        entity1.AddComponent(component1);
        entity2.AddComponent(component2);

        var entitiesWithBothComponents = _world.GetEntitiesWith<TestComponent, AnotherTestComponent>().ToList();
        Assert.That(entitiesWithBothComponents.Count, Is.EqualTo(0));
    }

    [Test]
    public void GetEntitiesWith_ShouldReturnOnlyEntitiesWithAllThreeComponents() {
        var entity1 = _world.CreateEntity<LSEntity>();
        var entity2 = _world.CreateEntity<LSEntity>();
        var entity3 = _world.CreateEntity<LSEntity>();

        var component1 = new TestComponent { Value = 10 };
        var component2 = new AnotherTestComponent { Value = 20 };
        var component3 = new ThirdTestComponent { Value = 30 };

        entity1.AddComponent(component1);
        entity1.AddComponent(component2);
        entity1.AddComponent(component3);

        entity2.AddComponent(component1);
        entity2.AddComponent(component2);

        entity3.AddComponent(component1);
        entity3.AddComponent(component3);

        var entitiesWithAllThree = _world.GetEntitiesWith<TestComponent, AnotherTestComponent, ThirdTestComponent>().ToList();

        Assert.That(entitiesWithAllThree.Count, Is.EqualTo(1));
        Assert.That(entitiesWithAllThree[0].ID, Is.EqualTo(entity1.ID));
    }
}
