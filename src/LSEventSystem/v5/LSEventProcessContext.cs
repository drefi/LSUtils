using System.Collections.Generic;
using System.Linq;

namespace LSUtils.EventSystem;

//internal record NodeResultKey(ILSEventNode? parent, ILSEventNode node);

public class LSEventProcessContext {

    public LSEvent Event { get; }

    // NOTE: giving the capability to create a complex node structure, the system won't need to have callbacks;
    // any sort desired behaviour should be possible using nodes only.
    private volatile bool _isCancelled = false;

    ILSEventNode _rootNode;

    public bool IsCancelled => _isCancelled;
    internal LSEventProcessContext(LSEvent @event, ILSEventNode rootNode) {
        Event = @event;
        _rootNode = rootNode;
    }

    internal LSEventProcessStatus Process() {
        System.Console.WriteLine($"[LSEventProcessContext] Processing root node '{_rootNode.NodeID}'...");
        var result = _rootNode.Process(this);
        if (result == LSEventProcessStatus.CANCELLED) {
            _isCancelled = true;
            System.Console.WriteLine($"[LSEventProcessContext] Root node processing was cancelled.");
        }
        if (result == LSEventProcessStatus.WAITING) {
            System.Console.WriteLine($"[LSEventProcessContext] Root node is waiting.");
        }
        return result;
    }

    public LSEventProcessStatus Resume(params string[]? nodes) {
        return _rootNode.Resume(this, nodes);
    }

    public LSEventProcessStatus Fail(params string[]? nodes) {
        return _rootNode.Fail(this, nodes);
    }
    public void Cancel() {
        var result = _rootNode.Cancel(this);
        if (result != LSEventProcessStatus.CANCELLED) {
            System.Console.WriteLine($"[LSEventProcessContext] Warning: Root node Cancel() did not return CANCELLED status.");
        }
            _isCancelled = true;
            System.Console.WriteLine($"[LSEventProcessContext] Root node processing was cancelled.");
    }
}
