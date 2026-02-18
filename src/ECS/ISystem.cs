namespace LSUtils.ECS;
/// <summary>
/// Base interface for systems in the ECS framework.
/// Systems contain the logic that processes entities and their components.
/// A system operates on entities that have a specific set of components.
/// </summary>
public interface ISystem {
    /// <summary>
    /// Unique name of the system.
    /// </summary>
    string SystemName { get; }

    /// <summary>
    /// Called once when the system is initialized in the world.
    /// </summary>
    void Initialize(IWorld world);

    /// <summary>
    /// Called every frame.
    /// Main system logic occurs here.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Called when the system is deactivated or destroyed.
    /// </summary>
    void Shutdown();
}
