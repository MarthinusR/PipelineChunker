using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static Mark2.Pipeline;

namespace Mark2 {
    public partial class Pipeline {
        private interface IConduit<ConduitT> : IEnumerable<Conduit<ConduitT>> where ConduitT : Conduit<ConduitT>, new() {
            ChannelClass<ConduitT> Channel { get; set; }

            int ChannelId { get; set; }
        }
    }
}
