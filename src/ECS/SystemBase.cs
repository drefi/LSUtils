namespace LSUtils.ECS;

/// <summary>
/// Classe base abstrata para sistemas.
/// Fornece implementação padrão de Initialize e Shutdown.
/// Subclasses devem implementar Update com a lógica específica do sistema.
/// </summary>
public abstract class SystemBase : ISystem {
    public abstract string SystemName { get; }
    protected IWorld? World { get; private set; }

    protected SystemBase() { }

    public virtual void Initialize(IWorld world) {
        World = world ?? throw new LSArgumentNullException(nameof(world));
    }

    public abstract void Update(float deltaTime);

    public virtual void Shutdown() {
        // Override se necessário
    }
}