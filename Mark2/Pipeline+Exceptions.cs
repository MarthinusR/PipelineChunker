using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using static Mark2.Pipeline;

namespace Mark2 {
    public partial class Pipeline {
        public class ConduitIterationException : Exception {
            public ConduitIterationException(string message) : base(message) { }
        }
        public class CompoundException<ConduitT> : Exception where ConduitT : Conduit<ConduitT>, new() {
            public CompoundException() { }
            public void Add(ConduitT conduit, Exception exception) {
                errorList.Add(new KeyValuePair<ConduitT, Exception>(conduit, exception));
            }
            public List<KeyValuePair<ConduitT, Exception>> errorList = new List<KeyValuePair<ConduitT, Exception>>();
            public override string Message => string.Join(Environment.NewLine, errorList.Select(x => $"Conduit of value [{x.Key}] threw an unhandled exception:{Environment.NewLine}  {x.Value.Message.Replace(Environment.NewLine, $"{Environment.NewLine}  ")}"));
            public override string ToString() => $"{string.Join(Environment.NewLine, errorList.Select(x => $"Conduit of value [{x.Key}] threw an unhandled exception:{Environment.NewLine}{x.Value.ToString().Replace(Environment.NewLine, ($"{Environment.NewLine}  "))}{Environment.NewLine}{base.StackTrace}"))}";
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
