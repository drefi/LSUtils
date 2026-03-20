namespace LSUtils;

public interface ILSVisitor {
    bool Visit<TValue>(out TValue? value, params object?[] args);
}
