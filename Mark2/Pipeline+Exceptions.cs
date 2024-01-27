using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static Mark2.Pipeline;

namespace Mark2 {
    public partial class Pipeline {
        public class ConduitIterationException : Exception {
            public ConduitIterationException(string message) : base(message) { }
        }
        public class MethodIsCapturingException<ConduitT> : Exception where ConduitT : Conduit<ConduitT>, new() {
            public MethodIsCapturingException(string additionalInfo, MethodInfo methodInfo) :
                base($"{typeof(ConduitT).FullName} is capturing variables{(string.IsNullOrEmpty(additionalInfo) ? "" : $" {additionalInfo}")}. [{methodInfo.Name}]") { }
        }

        public class InvalidChunkInvocation<ConduitT> : Exception where ConduitT : Conduit<ConduitT>, new() {
            public InvalidChunkInvocation(string additionalInfo) :
                base($"{typeof(ConduitT).FullName} is invoking Chunk incorrectly{(string.IsNullOrEmpty(additionalInfo) ? "" : $" {additionalInfo}")}.") { }
        }
        public class ChunkOperationException<ConduitT> : Exception where ConduitT : Conduit<ConduitT>, new() {
            public ChunkOperationException(string additionalInfo, Exception ex) :
                base($"{typeof(ConduitT).FullName}'s Chunk encountered an error{(string.IsNullOrEmpty(additionalInfo) ? "" : $" {additionalInfo}")}.", ex) { }
        }

        public class IterationException<ConduitT> : Exception where ConduitT : Conduit<ConduitT>, new() {
            public readonly ConduitT Conduit;
            public IterationException(ConduitT conduit, string message, Exception ex) : base(message, ex) => Conduit = conduit;
        }
        public class IterationInitializationException<ConduitT> : IterationException<ConduitT> where ConduitT : Conduit<ConduitT>, new() {
            public IterationInitializationException(ConduitT conduit, string message, Exception ex) : base(conduit, message, ex) { }
        }
    }
}
