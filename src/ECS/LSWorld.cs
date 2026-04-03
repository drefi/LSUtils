// LSUtils ECS is currently not functional, it needs too much work
/**
using System.Collections.Generic;
using System.Linq;

namespace LSUtils.ECS;

public class LSWorld {
    public EntityManager EntityManager { get; }

    public LSEntity Entities { get; }
    private readonly Dictionary<System.Type, ISystem> _systems = new();

    public LSWorld() {
        EntityManager = new EntityManager(this);
    }

    public void Destroy() {
        EntityManager.Destroy();
        foreach (var system in _systems.Values) {
            system.Shutdown();
        }

        _systems.Clear();
    }
    public void Update(float deltaTime) {
        foreach (var system in _systems.Values) {
            system.Update(deltaTime);
        }
    }

    #region Entity Querying


    #endregion

    #region System Management
    public ISystem? GetSystem(System.Type type) {
        if (_systems.TryGetValue(type, out var system))
            return system;

        throw new LSNullReferenceException($"System of type {type.Name} not found.");
    }
    public bool TryGetSystem<T>(out T? system) where T : ISystem {
        try {
            var existingSystem = GetSystem(typeof(T));
            if (existingSystem is T typedSystem) {
                system = typedSystem;
                return true;
            }
            system = default;
            return false;
        } catch (LSNullReferenceException) {
            system = default;
            return false;
        }
    }
    #endregion
}
**/
