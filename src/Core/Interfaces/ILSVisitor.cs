namespace LSUtils;

public interface ILSVisitor {
    bool Visit(params object?[] args);
}
