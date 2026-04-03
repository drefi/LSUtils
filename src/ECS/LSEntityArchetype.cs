// LSUtils ECS is currently not functional, it needs too much work
/**
using System.Collections.Generic;

namespace LSUtils.ECS;

public struct LSEntityArchetype {
    private readonly ComponentType[]? _componentTypes;

    public LSEntityArchetype(ComponentType[]? componentTypes) {
        _componentTypes = componentTypes;
    }
    public bool Equals(LSEntityArchetype other) {
        if (_componentTypes == null && other._componentTypes == null) {
            return true;
        }

        if (_componentTypes == null || other._componentTypes == null) {
            return false;
        }

        if (_componentTypes.Length != other._componentTypes.Length) {
            return false;
        }

        var components = new HashSet<ComponentType>();
        foreach (var componentType in _componentTypes) {
            if (!components.Add(componentType)) {
                return false;
            }
        }

        var otherComponents = new HashSet<ComponentType>();
        foreach (var componentType in other._componentTypes) {
            if (!otherComponents.Add(componentType)) {
                return false;
            }
        }

        return components.SetEquals(otherComponents);
    }
    override public bool Equals(object? obj) {
        if (obj is LSEntityArchetype other) {
            return Equals(other);
        }
        return false;
    }

    public override int GetHashCode() {
        if (_componentTypes == null) {
            return 0;
        }

        int sum = 0;
        int xor = 0;
        int count = 0;

        foreach (var componentType in _componentTypes) {
            int componentHash = componentType.GetHashCode();
            sum += componentHash;
            xor ^= componentHash;
            count++;
        }

        return System.HashCode.Combine(sum, xor, count);
    }

    public static bool operator ==(LSEntityArchetype left, LSEntityArchetype right) {
        return left.Equals(right);
    }

    public static bool operator !=(LSEntityArchetype left, LSEntityArchetype right) {
        return !left.Equals(right);
    }
    public ComponentType[]? GetComponentTypes() {
        return _componentTypes;
    }

}
**/
