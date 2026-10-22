namespace LSUtils.Spatial;

using System.Collections.Generic;
/// <summary>
/// Interface base para estruturas de indexação espacial.
/// </summary>
/// <typeparam name="T">Tipo dos objetos indexados.</typeparam>
public interface ISpatialIndex<T> where T : notnull {
    /// <summary>
    /// Insere um objeto no índice espacial.
    /// </summary>
    /// <param name="item">O objeto a ser inserido.</param>
    /// <param name="bounds">Os limites espaciais do objeto.</param>
    /// <returns>True se inserido com sucesso, false caso contrário.</returns>
    bool Insert(T item, LSBounds bounds);
    bool Update(T item, LSBounds bounds);

    /// <summary>
    /// Consulta objetos dentro de uma área específica.
    /// </summary>
    /// <param name="area">A área de consulta.</param>
    /// <param name="result">Resultado da consulta.</param>
    void Query(LSBounds area, ICollection<T> result, T[]? mask = null);

    bool TryGetBounds(T item, out LSBounds bounds);
    LSBounds GetBounds(T item);

    /// <summary>
    /// Remove um objeto do índice espacial.
    /// </summary>
    /// <param name="item">O objeto a ser removido.</param>
    /// <returns>True se removido com sucesso, false caso contrário.</returns>
    bool Remove(T item);

    /// <summary>
    /// Remove todos os objetos do índice espacial.
    /// </summary>
    void Clear();

    /// <summary>
    /// Número total de objetos no índice.
    /// </summary>
    int Count { get; }
}
