// LSUtils ECS is currently not functional, it needs too much work
/**
using System.Collections.Generic;

namespace LSUtils.ECS;

public interface ISystem {

    /// <summary>
    /// Called once when the system is initialized in the world.
    /// </summary>
    void Initialize(LSWorld world, params object?[] args);

    /// <summary>
    /// Called every frame.
    /// Main system logic occurs here.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Called when the system is deactivated or destroyed.
    /// </summary>
    void Shutdown();

    LSEntityQuery GetEntityQuery(params ComponentType[] componentTypes);
    LSEntityQuery GetEntityQuery(List<ComponentType> componentTypes);
    LSEntityQuery GetEntityQuery(params LSEntityQueryDesc[] queryDesc);
}

public class LSSystemBase : ISystem {

    public virtual void Initialize(LSWorld world, params object?[] args) {
    }

    public virtual void Update(float deltaTime) {
    }

    public virtual void Shutdown() {
    }

    public LSEntityQuery GetEntityQuery(params ComponentType[] componentTypes) {
        throw new System.NotImplementedException();
    }

    public LSEntityQuery GetEntityQuery(List<ComponentType> componentTypes) {
        throw new System.NotImplementedException();
    }

    public LSEntityQuery GetEntityQuery(params LSEntityQueryDesc[] queryDesc) {
        throw new System.NotImplementedException();
    }

}
**/
