using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public class ConduitInitializationException : Exception {
            public ConduitInitializationException(string message) : base(message) { }
        }
        public class ConduitIterationException : Exception {
            public ConduitIterationException(string message) : base(message) { }
        }
    }
}
