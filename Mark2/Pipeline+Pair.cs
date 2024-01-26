using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public struct Pair<ConduitT, T> where ConduitT : IConduit<ConduitT> {
            public readonly ConduitT Conduit;
            public T Value;

            public Pair(ConduitT conduit, T value) {
                Conduit = conduit;
                Value = value;
            }
        }
    }
}
