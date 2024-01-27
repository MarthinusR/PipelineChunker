using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static PipelineChunker.Pipeline;

namespace PipelineChunker {
    public partial class Pipeline {
        private interface IConduit<ConduitT> : IEnumerable<Conduit<ConduitT>> where ConduitT : Conduit<ConduitT>, new() {
            ChannelClass<ConduitT> Channel { get; set; }

            int ChannelId { get; set; }
        }
    }
}
