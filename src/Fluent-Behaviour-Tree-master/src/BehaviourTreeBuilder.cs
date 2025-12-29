using System.Collections.Generic;
using System.Linq;
using System.Text;
using LSUtils;

namespace FluentBehaviourTree {
    /// <summary>
    /// [DEPRECATED] Fluent API for building a behaviour tree.
    /// Use LSProcessSystem (LSProcessTreeBuilder) instead.
    /// This class will be removed - see MIGRATION_PLAN.md for migration guide.
    /// </summary>
    [System.Obsolete("Use LSProcessSystem instead. See MIGRATION_PLAN.md for migration guide.", false)]
    public class BehaviourTreeBuilder {
        /// <summary>
        /// Last node created.
        /// </summary>
        private IBehaviourTreeNode? curNode = null;

        /// <summary>
        /// Stack node nodes that we are build via the fluent API.
        /// </summary>
        private Stack<IParentBehaviourTreeNode> parentNodeStack = new Stack<IParentBehaviourTreeNode>();

        /// <summary>
        /// Create an action node.
        /// </summary>
        public BehaviourTreeBuilder Do(string name, System.Func<TimeData, BehaviourTreeStatus> fn) {
            if (parentNodeStack.Count <= 0) {
                throw new System.ApplicationException("Can't create an unnested LSActionNode, it must be a leaf node.");
            }

            var actionNode = new ActionNode(name, fn);
            parentNodeStack.Peek().AddChild(actionNode);
            return this;
        }

        /// <summary>
        /// Like an action node... but the function can return true/false and is mapped to success/failure.
        /// </summary>
        public BehaviourTreeBuilder Condition(string name, System.Func<TimeData, bool> fn) {
            return Do(name, t => fn(t) ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure);
        }

        /// <summary>
        /// Create an inverter node that inverts the success/failure of its children.
        /// </summary>
        public BehaviourTreeBuilder Inverter(string name) {
            var inverterNode = new InverterNode(name);

            if (parentNodeStack.Count > 0) {
                parentNodeStack.Peek().AddChild(inverterNode);
            }

            parentNodeStack.Push(inverterNode);
            return this;
        }

        /// <summary>
        /// Create a sequence node.
        /// </summary>
        public BehaviourTreeBuilder Sequence(string name) {
            var sequenceNode = new SequenceNode(name);

            if (parentNodeStack.Count > 0) {
                parentNodeStack.Peek().AddChild(sequenceNode);
            }

            parentNodeStack.Push(sequenceNode);
            return this;
        }

        /// <summary>
        /// Create a parallel node.
        /// </summary>
        public BehaviourTreeBuilder Parallel(string name, int numRequiredToFail, int numRequiredToSucceed) {
            var parallelNode = new ParallelNode(name, numRequiredToFail, numRequiredToSucceed);

            if (parentNodeStack.Count > 0) {
                parentNodeStack.Peek().AddChild(parallelNode);
            }

            parentNodeStack.Push(parallelNode);
            return this;
        }

        /// <summary>
        /// Create a selector node.
        /// </summary>
        public BehaviourTreeBuilder Selector(string name) {
            var selectorNode = new SelectorNode(name);

            if (parentNodeStack.Count > 0) {
                parentNodeStack.Peek().AddChild(selectorNode);
            }

            parentNodeStack.Push(selectorNode);
            return this;
        }

        /// <summary>
        /// Splice a sub tree into the parent tree.
        /// </summary>
        public BehaviourTreeBuilder Splice(IBehaviourTreeNode subTree) {
            if (subTree == null) {
                throw new System.ArgumentNullException("subTree");
            }

            if (parentNodeStack.Count <= 0) {
                throw new System.ApplicationException("Can't splice an unnested sub-tree, there must be a parent-tree.");
            }

            parentNodeStack.Peek().AddChild(subTree);
            return this;
        }

        /// <summary>
        /// Build the actual tree.
        /// </summary>
        public IBehaviourTreeNode Build() {
            if (curNode == null) {
                throw new System.ApplicationException("Can't create a behaviour tree with zero nodes");
            }
            return curNode;
        }

        /// <summary>
        /// Ends a sequence of children.
        /// </summary>
        public BehaviourTreeBuilder End() {
            curNode = parentNodeStack.Pop();
            return this;
        }
        public BehaviourTreeBuilder StateMachine<T>(string name, LSAction<T>? callback = null) where T : IFSMBehaviourTreeNode {
            var instance = System.Activator.CreateInstance(typeof(T), name);
            if (instance == null) {
                throw new System.InvalidOperationException($"Could not create an instance of type {typeof(T).FullName}.");
            }
            var stateMachineNode = (T)instance;
            LSAction<IFSMBehaviourTreeNode>? cb = callback == null ? null : new LSAction<IFSMBehaviourTreeNode>((IFSMBehaviourTreeNode smNode) => callback((T)smNode!));
            return StateMachine((IFSMBehaviourTreeNode)stateMachineNode, cb);
        }
        public BehaviourTreeBuilder StateMachine(IFSMBehaviourTreeNode stateMachineNode, LSAction<IFSMBehaviourTreeNode>? callback = null) {
            if (stateMachineNode == null) {
                throw new System.ArgumentNullException("stateMachineNode");
            }
            if (parentNodeStack.Count <= 0) {
                throw new System.ApplicationException("Can't create an unnested StateMachineNode, it must be a leaf node.");
            }
            parentNodeStack.Peek().AddChild(stateMachineNode);
            callback?.Invoke(stateMachineNode);
            return this;
        }
        public BehaviourTreeBuilder StateMachine(string name, out IFSMBehaviourTreeNode stateMachineNode, LSAction<IFSMBehaviourTreeNode>? callback = null) {
            stateMachineNode = new StateMachineNode(name);
            return StateMachine(stateMachineNode, callback);
        }
        public BehaviourTreeBuilder Transition<T>(IFSMBehaviourTreeNode? smNode, LSAction<T>? callback = null) where T : IBTStateNode {
            return Transition<T>(System.Guid.NewGuid().ToString(), smNode, callback);
        }
        public BehaviourTreeBuilder Transition<T>(string name, IFSMBehaviourTreeNode? smNode, LSAction<T>? callback = null) where T : IBTStateNode {
            LSAction<IBTStateNode> genericCallback = callback == null
                ? new LSAction<IBTStateNode>((IBTStateNode state) => { })
                : new LSAction<IBTStateNode>((IBTStateNode state) => callback((T)state!));
            if (smNode == null) throw new System.ArgumentNullException("smNode");
            var transitionNode = new TransitionNode(name, smNode, typeof(T), genericCallback);
            if (transitionNode == null) {
                throw new System.ArgumentNullException("transitionNode");
            }
            if (parentNodeStack.Count <= 0) {
                throw new System.ApplicationException("Can't create an unnested TransitionNode, it must be a leaf node.");
            }
            parentNodeStack.Peek().AddChild(transitionNode);
            return this;
        }
        public BehaviourTreeBuilder InState<T>(IFSMBehaviourTreeNode? smNode, LSAction<T>? callback = null) where T : IBTStateNode {
            return InState<T>(System.Guid.NewGuid().ToString(), smNode, callback);
        }
        public BehaviourTreeBuilder InState<T>(string name, IFSMBehaviourTreeNode? smNode, LSAction<T>? callback = null) where T : IBTStateNode {
            return Do(name, (t) => {
                if (smNode == null) throw new System.ArgumentNullException("smNode");
                var currentState = smNode.GetCurrentState();
                if (currentState is T) {
                    callback?.Invoke((T)currentState);
                    return BehaviourTreeStatus.Success;
                }
                return BehaviourTreeStatus.Failure;
            });
        }

    }
}
