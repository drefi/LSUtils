namespace LSUtils.ProcessSystem;

/// <summary>A typed view of the original execution context.</summary>
public sealed class LSProcessSession<TProcess> : LSProcessSession where TProcess : LSProcess {
    public new TProcess Process => (TProcess)base.Process;
    internal LSProcessSession(LSProcessSession session) : base(session) { }
}
