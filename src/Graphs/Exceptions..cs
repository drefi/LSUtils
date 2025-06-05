using System;

namespace LSUtils.Graphs {
    public class NonMemberNodeException : ArgumentException {

        public NonMemberNodeException() : this("Node must be a member of the graph.") {

        }

        public NonMemberNodeException(string message) : base(message) {

        }

    }
}
