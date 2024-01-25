using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public class ConduitInitializationException : Exception {
            public ConduitInitializationException(string message) : base(message) { }
        }
        public class ConduitIterationException : Exception {
            public ConduitIterationException(string message) : base(message) { }
        }
        public class MethodIsCapturingException<ConduitT> : Exception where ConduitT : IConduit<ConduitT> {
            public MethodIsCapturingException(string additionalInfo, MethodInfo methodInfo) : 
                base($"{typeof(ConduitT).FullName} is capturing variables{(string.IsNullOrEmpty(additionalInfo) ? "" : $" {additionalInfo}")}. [{methodInfo.Name}]") { }
        }

        public class InvalidChunkInvocation<ConduitT> : Exception where ConduitT: IConduit<ConduitT> {
            public InvalidChunkInvocation(string additionalInfo) :
                base($"{typeof(ConduitT).FullName} is invoking Chunk incorrectly{(string.IsNullOrEmpty(additionalInfo) ? "" : $" {additionalInfo}")}.") { }
        }
    }
}
