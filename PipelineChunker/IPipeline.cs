using System;
using System.Collections.Generic;
using System.Text;
using static PipelineChunker.Pipeline;

namespace PipelineChunker {
    public interface IPipeline {
        void GetChannelState<ConduitT>(ref bool IsOpen, ref bool IsChanneling);
        bool IsOpen { get; }

        int MaxChunkSize { get; }

        IEnumerable<int> ValidIds { get; }
    }
}
