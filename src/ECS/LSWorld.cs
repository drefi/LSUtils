using System.Collections.Generic;
using System.Linq;

namespace LSUtils.ECS;

public class LSWorld : IWorld {
    private readonly Dictionary<System.Guid, IEntity> _entities = new();
    private readonly Dictionary<System.Type, ISystem> _systems = new();

    public TEntity CreateEntity<TEntity>() where TEntity : IEntity, new() {
        var entity = new TEntity();
        _entities[entity.ID] = entity;
        return entity;
    }

    public TEntity CreateEntity<TEntity>(System.Guid id, string? name = null) where TEntity : IEntity, new() {
        if (_entities.ContainsKey(id))
            throw new LSInvalidOperationException($"Entity with ID {id} already exists.");

        var entity = (TEntity)System.Activator.CreateInstance(typeof(TEntity), id, name)!;
        if (entity == null)
            throw new LSInvalidOperationException($"Could not create entity of type {typeof(TEntity).Name}.");
        _entities[id] = entity;
        return entity;
    }

    public bool DestroyEntity(System.Guid entityId) {
        if (_entities.TryGetValue(entityId, out var entity)) {
            return _entities.Remove(entityId);
        }
        return false;
    }

    public IEnumerable<IEntity> GetEntitiesWith<T1>() where T1 : IComponent {
        return _entities.Values.Where(e => e.HasComponent<T1>());
    }

    public IEnumerable<IEntity> GetEntitiesWith<T1>(out IEnumerable<T1?> components) where T1 : IComponent {
        var result = GetEntitiesWith<T1>().ToList();
        components = result.Select(e => e.GetComponent<T1>()).ToList();
        return result;
    }

    public IEnumerable<IEntity> GetEntitiesWith<T1, T2>()
        where T1 : IComponent
        where T2 : IComponent {
        return _entities.Values.Where(e => e.HasComponent<T1>() && e.HasComponent<T2>());
    }

    public IEnumerable<IEntity> GetEntitiesWith<T1, T2, T3>()
        where T1 : IComponent
        where T2 : IComponent
        where T3 : IComponent {
        return _entities.Values.Where(e => e.HasComponent<T1>() && e.HasComponent<T2>() && e.HasComponent<T3>());
    }

    public IEntity GetEntity(System.Guid entityId) {
        if (_entities.TryGetValue(entityId, out var entity))
            return entity;

        throw new LSNullReferenceException($"Entity with ID {entityId} not found.");
    }

    public T GetSystem<T>() where T : ISystem {
        var type = typeof(T);
        if (_systems.TryGetValue(type, out var system))
            return (T)system;

        throw new LSNullReferenceException($"System of type {type.Name} not found.");
    }

    public void RegisterSystem(ISystem system) {
        var type = system.GetType();
        if (_systems.ContainsKey(type))
            throw new LSInvalidOperationException($"System of type {type.Name} is already registered.");

        _systems[type] = system;
    }

    public bool TryGetEntity(System.Guid entityId, out IEntity? entity) {
        return _entities.TryGetValue(entityId, out entity);
    }

    public bool TryGetSystem<T>(out T system) where T : ISystem {
        var type = typeof(T);
        if (_systems.TryGetValue(type, out var sys)) {
            system = (T)sys;
            return true;
        }
        system = default!;
        return false;
    }

    public bool UnregisterSystem<T>() where T : ISystem {
        var type = typeof(T);
        if (_systems.ContainsKey(type)) {
            _systems.Remove(type);
            return true;
        }
        return false;
    }

    public void Update(float deltaTime) {
        foreach (var system in _systems.Values) {
            system.Update(deltaTime);
        }
    }
}
