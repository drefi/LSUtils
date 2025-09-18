using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

//internal record NodeResultKey(ILSEventNode? parent, ILSEventNode node);

public class LSEventProcessContext {

    public LSEvent Event { get; }

    // tracking the node results in the context is starting to became too complex, going to delegate to the node instead
    //private readonly Dictionary<NodeResultKey, LSEventProcessStatus> _handlerNodeResults = new();
    private readonly List<LSAction<LSEventProcessContext>> _onSuccessCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onFailureCallbacks = new();
    private readonly List<LSAction<LSEventProcessContext>> _onCancelCallbacks = new();
    private volatile bool _isCancelled = false;

    ILSEventNode _rootNode;

    public bool IsCancelled => _isCancelled;
    internal LSEventProcessContext(LSEvent @event, ILSEventNode rootNode) {
        Event = @event;
        _rootNode = rootNode;
    }

    internal LSEventProcessStatus Process() {
        var result = _rootNode.Process(this);
        if (result == LSEventProcessStatus.WAITING) {
            System.Console.WriteLine($"[LSEventProcessContext] Root node is waiting.");
        }
        return result;
    }

    public LSEventProcessStatus Resume(string[] nodes) {
        return _rootNode.Resume(this, nodes);
    }

    public LSEventProcessStatus Fail(string[] nodes) {
        return _rootNode.Fail(this, nodes);
    }
    public void Cancel() {
        _rootNode.Cancel(this);
    }
}
