namespace LSUtils.ECS;

/// <summary>
/// Interface base para todos os componentes.
/// Componentes são estruturas de dados puras que armazenam estado.
/// Não contêm lógica de comportamento - essa responsabilidade é dos Sistemas.
/// </summary>
public interface IComponent {
    string ComponentName { get; }
}