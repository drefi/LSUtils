/**
namespace LSUtils.Tests.ECS;

using System.Linq;
using LSUtils.ECS;
using NUnit.Framework;

[TestFixture]
public class WorldTest {

    #region Mock Classes

    public class TestComponent : IComponent {
        public int Value { get; set; }
    }
    public class AnotherTestComponent : IComponent {
        public int Value { get; set; }
    }
    public class ThirdTestComponent : IComponent {
        public int Value { get; set; }

    }
    public class TestSystem : ISystem {
        public string SystemName => throw new System.NotImplementedException();

        public void Initialize(LSWorld world, params object?[] args) {
            throw new System.NotImplementedException();
        }

        public void Shutdown() {
            throw new System.NotImplementedException();
        }

        public void Update(float deltaTime) {
            throw new System.NotImplementedException();
        }
    }
    public class CountingSystem : ISystem {
        public string SystemName => "CountingSystem";
        public int UpdateCount { get; private set; }
        LSWorld? _world;



        public void Update(float deltaTime) {
            UpdateCount++;
        }

        public void Shutdown() {
        }

        public void Initialize(LSWorld world, params object?[] args) {
            _world = world;
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
        var entity = _world.EntityManager.CreateEntity();
        var retrievedEntity = _world.EntityManager.GetEntity(entity.Index);
        Assert.That(retrievedEntity.Index, Is.EqualTo(entity.Index));
        Assert.That(retrievedEntity, Is.EqualTo(entity));
    }

    [Test]
    public void DestroyEntity_ShouldRemoveEntityFromWorld() {
        var entity = _world.EntityManager.CreateEntity();
        _world.EntityManager.DestroyEntity(entity);
        Assert.Throws<System.InvalidOperationException>(() => _world.EntityManager.GetEntity(entity.Index));
    }

    [Test]
    public void GetEntitiesWith_ShouldReturnCorrectEntities() {
        var e1 = _world.EntityManager.CreateEntity();
        var e2 = _world.EntityManager.CreateEntity();
        var component1 = new TestComponent { Value = 10 };
        var component2 = new TestComponent { Value = 20 };
        _world.EntityManager.AddComponent(e1, new ComponentType(component1));
        _world.EntityManager.AddComponent(e2, new ComponentType(component2));
        _world.EntityManager.En
        var entities = _world.GetEntitiesWith<TestComponent>(out var components).ToList();
        Assert.That(entities.Count, Is.EqualTo(2));
        Assert.That(components, Is.Not.Null);
        Assert.That(components.Count(), Is.EqualTo(2));
        Assert.That(components.Any(c => c != null && c.Value == 10), Is.True);
        Assert.That(components.Any(c => c != null && c.Value == 20), Is.True);
    }
    [Test]
    public void GetEntitiesWith_ShouldReturnEmptyWhenNoEntitiesHaveComponent() {
        var entities = _world.GetEntitiesWith<TestComponent>(out var components).ToList();
        Assert.That(entities.Count, Is.EqualTo(0));
        Assert.That(components, Is.Not.Null);
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
**/
