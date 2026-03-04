namespace LSUtils.ECS;

using System.Collections.Generic;
/// <summary>
/// Interface para o mundo ECS.
/// O mundo gerencia entidades e sistemas.
/// É responsável por coordenar a execução dos sistemas sobre as entidades.
/// </summary>
public interface IWorld {
    /// <summary>
    /// Cria uma nova entidade no mundo.
    /// </summary>
    TEntity CreateEntity<TEntity>() where TEntity : IEntity, new();

    /// <summary>
    /// Cria uma entidade com um ID específico.
    /// </summary>
    TEntity CreateEntity<TEntity>(System.Guid id, string? name = null) where TEntity : IEntity, new();

    /// <summary>
    /// Remove uma entidade do mundo.
    /// </summary>
    bool DestroyEntity(System.Guid entityId);

    /// <summary>
    /// Obtém uma entidade pelo ID.
    /// </summary>
    IEntity GetEntity(System.Guid entityId);

    bool TryGetEntity(System.Guid entityId, out IEntity? entity);

    /// <summary>
    /// Obtém todas as entidades que possuem um conjunto específico de componentes.
    /// </summary>
    IEnumerable<IEntity> GetEntitiesWith<TComponent>() where TComponent : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<TComponent>(out IEnumerable<TComponent?>? components) where TComponent : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<TComponent, TComponent2>() where TComponent : IComponent where TComponent2 : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<TComponent, TComponent2, TComponent3>() where TComponent : IComponent where TComponent2 : IComponent where TComponent3 : IComponent;

    /// <summary>
    /// Registra um sistema no mundo.
    /// </summary>
    void RegisterSystem(ISystem system);

    /// <summary>
    /// Remove um sistema do mundo.
    /// </summary>
    bool UnregisterSystem<T>() where T : ISystem;

    /// <summary>
    /// Obtém um sistema pelo nome.
    /// </summary>
    T GetSystem<T>() where T : ISystem;

    bool TryGetSystem<T>(out T? system) where T : ISystem;

    /// <summary>
    /// Executa todos os sistemas registrados.
    /// </summary>
    void Update(float deltaTime);
}
