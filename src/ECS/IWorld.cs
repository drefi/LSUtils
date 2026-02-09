namespace LSUtils.ECS;

using System;
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
    TEntity CreateEntity<TEntity>(Guid id, string? name = null) where TEntity : IEntity, new();

    /// <summary>
    /// Remove uma entidade do mundo.
    /// </summary>
    bool DestroyEntity(Guid entityId);

    /// <summary>
    /// Obtém uma entidade pelo ID.
    /// </summary>
    IEntity GetEntity(Guid entityId);

    bool TryGetEntity(Guid entityId, out IEntity? entity);

    /// <summary>
    /// Obtém todas as entidades que possuem um conjunto específico de componentes.
    /// </summary>
    IEnumerable<IEntity> GetEntitiesWith<T1>() where T1 : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<T1>(out IEnumerable<T1>? components) where T1 : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<T1, T2>() where T1 : IComponent where T2 : IComponent;
    IEnumerable<IEntity> GetEntitiesWith<T1, T2, T3>() where T1 : IComponent where T2 : IComponent where T3 : IComponent;

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

    bool TryGetSystem<T>(out T system) where T : ISystem;

    /// <summary>
    /// Executa todos os sistemas registrados.
    /// </summary>
    void Update(float deltaTime);
}
