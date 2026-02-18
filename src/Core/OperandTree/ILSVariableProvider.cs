namespace LSUtils;

public interface ILSVariableProvider {
    // ID: "Source", Key: AttributeDefiner asset
    object GetValue(string id, object key);
    T GetValue<T>(string id, object key) => (T)GetValue(id, key);
}
