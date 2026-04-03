// LSUtils ECS is currently not functional, it needs too much work
/**
namespace LSUtils.ECS;

using System;
using System.Collections.Generic;
using System.Linq;

public struct EntityManager {
    public record struct EntityLocation(LSEntityArchetype Archetype, int Index, int Version = 0);
    private readonly Dictionary<LSEntityArchetype, List<LSEntity>> _entitiesByArchetype = new();
    private readonly Dictionary<LSEntity, Dictionary<ComponentType, IComponent>> _components = new();
    private readonly Dictionary<LSEntity, EntityLocation> _entityLocations = new();

    public LSWorld World { get; }
    public LSEntityQuery UniversalQuery { get; private set; }
    private int _nextEntityIndex = 0;

    public EntityManager(LSWorld world) {
        World = world;
    }

    public LSEntity CreateEntity(LSEntityArchetype archetype) {
        var entity = new LSEntity { Index = _nextEntityIndex++, Version = 0 };
        SetArchetype(entity, archetype);
        return entity;
    }
    public LSEntity CreateEntity(params ComponentType[] componentTypes) {
        var entity = new LSEntity { Index = _nextEntityIndex++, Version = 0 };
        var archetype = new LSEntityArchetype(componentTypes);
        for (int i = 0; i < componentTypes?.Length; i++) {
            AddComponent(entity, componentTypes[i]);
        }
        return entity;
    }
    public LSEntity CreateEntity() {
        return CreateEntity(System.Array.Empty<ComponentType>());
    }
    public void CreateEntity(LSEntityArchetype archetype, LSEntity[] entities) {
        if (entities == null || entities.Length == 0) {
            throw new LSException("Entities array cannot be null or empty.");
        }
        for (int i = 0; i < entities.Length; i++) {
            entities[i] = CreateEntity(archetype);
        }
    }
    public void CreateEntity(LSEntityArchetype archetype, int count) {
        if (count <= 0) {
            throw new LSException("Count must be greater than zero.");
        }
        for (int i = 0; i < count; i++) {
            CreateEntity(archetype);
        }
    }

    public void SetArchetype(LSEntity entity, LSEntityArchetype archetype) {

        if (_entityLocations.TryGetValue(entity, out var oldLocation)) {
            var oldArchetype = oldLocation.Archetype;
            if (oldArchetype == archetype) {
                return; // No change needed
            }
            if (_entitiesByArchetype.TryGetValue(oldArchetype, out var oldEntityList)) {
                oldEntityList.RemoveAt(oldLocation.Index);
                if (oldEntityList.Count == 0) {
                    _entitiesByArchetype.Remove(oldArchetype);
                }
                _entityLocations.Remove(entity);
            }
        }
        if (!_entitiesByArchetype.TryGetValue(archetype, out var entityList)) {
            entityList = new List<LSEntity>();
            _entitiesByArchetype[archetype] = entityList;
        }
        entityList.Add(entity);
        _entityLocations[entity] = new EntityLocation(archetype, entityList.Count - 1);
    }
    public LSEntityArchetype GetArchetype(LSEntity entity) {
        if (_entityLocations.TryGetValue(entity, out var location)) {
            return location.Archetype;
        }
        throw new System.InvalidOperationException("Entity not found.");
    }


    public void AddComponent(LSEntity entity, ComponentType componentType) {
        if (!_components.TryGetValue(entity, out var componentDict)) {
            componentDict = new Dictionary<ComponentType, IComponent>();
            _components[entity] = componentDict;
        }
        if (!componentDict.ContainsKey(componentType)) {
            var component = (IComponent)System.Activator.CreateInstance(componentType.Type)!;
            componentDict[componentType] = component;
        }
        var archetype = GetArchetype(entity);
        var newComponentTypes = archetype.GetComponentTypes()?.Append(componentType).ToArray() ?? new[] { componentType };
        var newArchetype = new LSEntityArchetype(newComponentTypes);
        SetArchetype(entity, newArchetype);
    }

    public TComponent GetComponent<TComponent>(LSEntity entity, ComponentType componentType) where TComponent : IComponent {
        if (_components.TryGetValue(entity, out var componentDict)) {
            if (componentDict.TryGetValue(componentType, out var component)) {
                return (TComponent)component;
            }
        }
        throw new System.InvalidOperationException("Component not found.");
    }
    public bool RemoveComponent(LSEntity entity, ComponentType componentType) {
        if (_components.TryGetValue(entity, out var componentDict)) {
            if (componentDict.Remove(componentType)) {
                var archetype = GetArchetype(entity);
                var newComponentTypes = archetype.GetComponentTypes()?.Where(ct => ct != componentType).ToArray() ?? Array.Empty<ComponentType>();
                var newArchetype = new LSEntityArchetype(newComponentTypes);
                SetArchetype(entity, newArchetype);
                return true;
            }
        }
        return false;
    }

    public void DestroyEntity(LSEntity entity) {
        if (_entityLocations.TryGetValue(entity, out var location)) {
            var archetype = location.Archetype;
            if (_entitiesByArchetype.TryGetValue(archetype, out var entityList)) {
                entityList.RemoveAt(location.Index);
                if (entityList.Count == 0) {
                    _entitiesByArchetype.Remove(archetype);
                }
            }
            _entityLocations.Remove(entity);
            _components.Remove(entity);
        }
    }

    public void Destroy() {
        _entitiesByArchetype.Clear();
        _components.Clear();
        _entityLocations.Clear();
    }

}
public struct ForEachLambda {
    public void ForEach() {

    }
}
public struct LSEntityQuery {
    public ComponentType[] ComponentTypes { get; }
    public LSEntityQuery(params ComponentType[] componentTypes) {
        ComponentTypes = componentTypes;
    }
    public LSEntity[] ToEntityArray() {
        return Array.Empty<LSEntity>();
    }
}
public struct LSEntityQueryDesc {
    public ComponentType[] Any { get; }
    public ComponentType[] None { get; }
    public ComponentType[] All { get; }
    public LSEntityQueryDesc(ComponentType[]? any = null, ComponentType[]? none = null, ComponentType[]? all = null) {
        Any = any ?? Array.Empty<ComponentType>();
        None = none ?? Array.Empty<ComponentType>();
        All = all ?? Array.Empty<ComponentType>();
    }
}
public ref struct LSEntityQueryBuilder {
    private List<ComponentType> _any = new();
    private List<ComponentType> _none = new();
    private List<ComponentType> _all = new();

    public LSEntityQueryBuilder() {
    }

    public LSEntityQueryBuilder WithAbsent<T1>() {
        return this;
    }
    public LSEntityQueryBuilder WithAny<T1>() {
        return this;
    }
    public LSEntityQueryBuilder WithAll<T1>() {
        return this;
    }
    public LSEntityQueryBuilder WithNone<T1>() {
        return this;
    }
    public LSEntityQuery Build(EntityManager entityManager) {
        return new LSEntityQuery();
    }

}
**/
