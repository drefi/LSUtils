namespace LSUtils.EventSystem;

public interface ILSEventable {
    public System.Guid ID { get; }
    LSEventProcessStatus Initialize(LSEventContextDelegate? ctxBuilder = null, LSEventContextManager? manager = null);
}
