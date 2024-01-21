using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public interface IPipeline {
        Pipeline.IChannelState Bind<ConduitT>();
        void GetChannelState<ConduitT>(ref bool IsOpen, ref bool IsChanneling);
        bool IsOpen { get; }
    }
}
