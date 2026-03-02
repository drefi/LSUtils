namespace LSUtils;

public interface ILSAcceptor {
    bool Accept<TValue>(ILSVisitor visitor, out TValue? value, params object?[] parameters);
}
