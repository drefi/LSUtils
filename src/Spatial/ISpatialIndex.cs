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
    bool Insert(T item, Bounds bounds);

    /// <summary>
    /// Consulta objetos dentro de uma área específica.
    /// </summary>
    /// <param name="area">A área de consulta.</param>
    /// <returns>Lista de objetos dentro da área.</returns>
    IReadOnlyList<T> Query(Bounds area);

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

/// <summary>
/// Representa os limites retangulares de um objeto no espaço 2D.
/// </summary>
/// <param name="X">Coordenada X do centro.</param>
/// <param name="Y">Coordenada Y do centro.</param>
/// <param name="Width">Largura do retângulo.</param>
/// <param name="Height">Altura do retângulo.</param>
public readonly record struct Bounds(float X, float Y, float Width, float Height) {
    /// <summary>
    /// Coordenada X mínima.
    /// </summary>
    public float MinX => X - Width / 2;

    /// <summary>
    /// Coordenada X máxima.
    /// </summary>
    public float MaxX => X + Width / 2;

    /// <summary>
    /// Coordenada Y mínima.
    /// </summary>
    public float MinY => Y - Height / 2;

    /// <summary>
    /// Coordenada Y máxima.
    /// </summary>
    public float MaxY => Y + Height / 2;

    /// <summary>
    /// Verifica se este bounds contém um ponto.
    /// </summary>
    public bool Contains(float x, float y) {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>
    /// Verifica se este bounds contém outro bounds completamente.
    /// </summary>
    public bool Contains(Bounds other) {
        return other.MinX >= MinX && other.MaxX <= MaxX &&
               other.MinY >= MinY && other.MaxY <= MaxY;
    }

    /// <summary>
    /// Verifica se este bounds intersecta com outro bounds.
    /// </summary>
    public bool Intersects(Bounds other) {
        return !(other.MinX > MaxX || other.MaxX < MinX ||
                 other.MinY > MaxY || other.MaxY < MinY);
    }
}
