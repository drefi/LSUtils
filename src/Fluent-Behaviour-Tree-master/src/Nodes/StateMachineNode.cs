using System.Collections.Generic;
using System.Linq;
using System.Text;
using LSUtils;

namespace FluentBehaviourTree {
    public interface IFSMBehaviourTreeNode : IBehaviourTreeNode {
        void AddState(IBTStateNode state);
        void Transition(System.Type type, LSAction<IBTStateNode>? stateCallback = null, LSAction<IBTStateNode>? onExitCallback = null);
        void Transition<T>(LSAction<T>? stateCallback = null, LSAction<T>? onExitCallback = null) where T : IBTStateNode;
        IBTStateNode? GetCurrentState();
    }
    public interface IBTStateNode : IBehaviourTreeNode {
        void Enter(LSAction<IBTStateNode>? onEnterState = null, LSAction<IBTStateNode>? onExitState = null);
        void Exit(LSAction? callback = null);

    }
    public class TransitionNode : IBehaviourTreeNode {
        private string _name;
        IFSMBehaviourTreeNode _stateMachineNode;
        LSAction<IBTStateNode> cb;
        System.Type _stateType;
        public TransitionNode(string name, IFSMBehaviourTreeNode stateMachine, System.Type stateType, LSAction<IBTStateNode> callback) {
            _name = name;
            _stateMachineNode = stateMachine;
            _stateType = stateType;
            cb = callback;
        }

        public BehaviourTreeStatus Update(TimeData time) {
            _stateMachineNode.Transition(_stateType, cb);
            return BehaviourTreeStatus.Success;
        }
    }
    public abstract class StateMachineNode<T> : StateMachineNode where T : IBTStateNode {
        public StateMachineNode(string name) : base(name) { }
    }
    public class StateMachineNode : IFSMBehaviourTreeNode {
        protected string name;

        protected Dictionary<System.Type, IBTStateNode> _states = new Dictionary<System.Type, IBTStateNode>();
        protected IBTStateNode? _currentState;
        public StateMachineNode(string name) {
            this.name = name;
        }

        public BehaviourTreeStatus Update(TimeData time) {
            var parentName = time.nodeName;
            time.nodeName = $"{(string.IsNullOrEmpty(parentName) ? "" : $"{parentName}::")}{name}::";
            if (_currentState != null) return _currentState.Update(time);
            return BehaviourTreeStatus.Failure;
        }
        public void AddState(IBTStateNode state) {
            System.Type type = state.GetType();
            _states.Add(type, state);
            if (_currentState == null) Transition(type);
        }
        public virtual void Transition(System.Type nodeType, LSAction<IBTStateNode>? stateCallback = null, LSAction<IBTStateNode>? onExitCallback = null) {
            if (typeof(IBTStateNode).IsAssignableFrom(nodeType) == false) throw new LSArgumentException("nodeSystem.Type must be an IBTStateNode");
            var state = _states[nodeType];
            if (state == _currentState) {
                stateCallback?.Invoke(_currentState);
                return;
            }

            LSAction cb = () => {
                _currentState = _states[nodeType];
                _currentState.Enter(stateCallback, onExitCallback);
            };
            if (_currentState != null) {
                IBTStateNode oldState = _currentState;
                oldState.Exit(cb);
            } else cb();
        }
        public virtual void Transition<T>(LSAction<T>? stateCallback = null, LSAction<T>? onExitCallback = null) where T : IBTStateNode {
            LSAction<IBTStateNode>? stateCB = stateCallback == null ? null : new LSAction<IBTStateNode>((s) => stateCallback((T)s!));
            LSAction<IBTStateNode>? exitCB = onExitCallback == null ? null : new LSAction<IBTStateNode>((s) => onExitCallback((T)s!));
            Transition(typeof(T), stateCB, exitCB);
        }
        public IBTStateNode? GetCurrentState() {
            return _currentState ?? null;
        }
    }
}
