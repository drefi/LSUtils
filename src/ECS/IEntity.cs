namespace LSUtils.ECS;

using System;
using System.Collections.Generic;
/// <summary>
/// Interface base para entidades.
/// Uma entidade é um contenedor de componentes.
/// A entidade em si é apenas um identificador - toda lógica está nos componentes e sistemas.
/// </summary>
public interface IEntity {
    /// <summary>
    /// Identificador único da entidade.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    /// Adiciona um componente à entidade.
    /// </summary>
    void AddComponent<T>(T component) where T : IComponent;

    /// <summary>
    /// Remove um componente da entidade.
    /// </summary>
    bool RemoveComponent<T>() where T : IComponent;

    /// <summary>
    /// Obtém um componente da entidade.
    /// </summary>
    T? GetComponent<T>() where T : IComponent;

    /// <summary>
    /// Tenta obter um componente da entidade.
    /// </summary>
    bool TryGetComponent<T>(out T component) where T : IComponent;

    /// <summary>
    /// Verifica se a entidade possui um componente específico.
    /// </summary>
    bool HasComponent<T>() where T : IComponent;

    /// <summary>
    /// Obtém todos os componentes da entidade.
    /// </summary>
    IEnumerable<IComponent> GetAllComponents();
}