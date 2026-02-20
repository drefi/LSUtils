using System.Collections.Generic;

namespace LSUtils;
/// <summary>
/// Interface for variable operands that retrieve their value from a provider
/// </summary>
public interface ILSVarOperand : ILSOperand {
    IReadOnlyList<object?> Parameters { get; }
}
