namespace LSUtils;

public interface ILSVariableProvider<out T> {
    // ID: "Source", Key: AttributeDefiner asset
    T GetValue(string id, object key);
}
