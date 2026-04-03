// LSUtils ECS is currently not functional, it needs too much work
/**
namespace LSUtils.ECS;

public struct ComponentType {
    public System.Type Type { get; }
    public ComponentType(IComponent component) {
        Type = component.GetType();
    }
    public ComponentType(System.Type type) {
        if (!typeof(IComponent).IsAssignableFrom(type)) {
            throw new System.ArgumentException($"Type {type.FullName} does not implement IComponent.");
        }
        Type = type;
    }
    public bool Equals(ComponentType other) {
        return Type == other.Type;
    }
    public override bool Equals(object? obj) {
        if (obj is ComponentType other) {
            return Equals(other);
        }
        return false;
    }
    public override int GetHashCode() {
        return Type.GetHashCode();
    }

    public static bool operator ==(ComponentType left, ComponentType right) {
        return left.Equals(right);
    }

    public static bool operator !=(ComponentType left, ComponentType right) {
        return !left.Equals(right);
    }

}
**/
