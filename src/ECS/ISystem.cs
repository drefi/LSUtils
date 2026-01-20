namespace LSUtils.ECS;
/// <summary>
/// Interface base para sistemas.
/// Sistemas contêm a lógica que processa entidades e seus componentes.
/// Um sistema opera sobre entidades que possuem um conjunto específico de componentes.
/// </summary>
public interface ISystem {
    /// <summary>
    /// Nome único do sistema.
    /// </summary>
    string SystemName { get; }

    /// <summary>
    /// Chamado uma vez quando o sistema é inicializado no mundo.
    /// </summary>
    void Initialize(IWorld world);

    /// <summary>
    /// Chamado a cada frame ou tick.
    /// Aqui ocorre a lógica principal do sistema.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Chamado quando o sistema é desativado ou destruído.
    /// </summary>
    void Shutdown();
}