namespace LSUtils.ECS;
using System;
using System.Collections.Generic;
/// <summary>
/// Implementação padrão de uma entidade ECS.
/// Gerencia componentes associados a esta entidade.
/// </summary>
public class Entity : IEntity {
    public Guid ID { get; private set; }

    private Dictionary<Type, IComponent> _components = new();

    public Entity(Guid? id = null) {
        ID = id ?? Guid.NewGuid();
    }

    public void AddComponent<T>(T component) where T : IComponent {
        if (component == null)
            throw new LSArgumentNullException(nameof(component));

        var type = typeof(T);
        if (_components.ContainsKey(type))
            throw new InvalidOperationException($"Entity already has component of type {type.Name}");

        _components[type] = component;
    }

    public bool RemoveComponent<T>() where T : IComponent {
        return _components.Remove(typeof(T));
    }

    public T GetComponent<T>() where T : IComponent {
        var type = typeof(T);
        if (_components.TryGetValue(type, out var component))
            return (T)component;

        throw new KeyNotFoundException($"Entity does not have component of type {type.Name}");
    }

    public bool HasComponent<T>() where T : IComponent {
        return _components.ContainsKey(typeof(T));
    }

    public IEnumerable<IComponent> GetAllComponents() {
        return _components.Values;
    }

    public bool TryGetComponent<T>(out T component) where T : IComponent {
        if (_components.TryGetValue(typeof(T), out var comp)) {
            component = (T)comp;
            return true;
        }
        component = default!;
        return false;
    }
}