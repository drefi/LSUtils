namespace LSUtils.Spatial;

/// <summary>
/// Representa os limites retangulares de um objeto no espaço 2D.
/// </summary>
/// <param name="X">Coordenada X do centro.</param>
/// <param name="Y">Coordenada Y do centro.</param>
/// <param name="Width">Largura do retângulo.</param>
/// <param name="Height">Altura do retângulo.</param>
public readonly record struct LSBounds(float X, float Y, float Width, float Height) {

    /// <summary>
    /// Coordenada X mínima.
    /// </summary>
    public readonly float MinX = X - Width / 2;

    /// <summary>
    /// Coordenada X máxima.
    /// </summary>
    public readonly float MaxX = X + Width / 2;

    /// <summary>
    /// Coordenada Y mínima.
    /// </summary>
    public readonly float MinY = Y - Height / 2;

    /// <summary>
    /// Coordenada Y máxima.
    /// </summary>
    public readonly float MaxY = Y + Height / 2;

    /// <summary>
    /// Verifica se este bounds contém um ponto.
    /// </summary>
    public bool Contains(float x, float y) {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    /// <summary>
    /// Verifica se este bounds contém outro bounds completamente.
    /// </summary>
    public bool Contains(LSBounds other) {
        return other.MinX >= MinX && other.MaxX <= MaxX &&
               other.MinY >= MinY && other.MaxY <= MaxY;
    }

    /// <summary>
    /// Verifica se este bounds intersecta com outro bounds.
    /// </summary>
    public bool Intersects(LSBounds other) {
        return !(other.MinX > MaxX || other.MaxX < MinX ||
                 other.MinY > MaxY || other.MaxY < MinY);
    }
}
