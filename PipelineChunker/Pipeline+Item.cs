using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        public struct Item<ConduitT, T> where ConduitT : Conduit<ConduitT>, new() {
            public readonly ConduitT Conduit;
            public Exception Exception;
            public T Value;


            public Item(ConduitT conduit, Exception exception, T value) {
                Conduit = conduit;
                Exception = exception;
                Value = value;
            }
        }
    }
}
