using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public interface IPipeline {
        void Bind<ConduitT>(ref ConduitT conduitT, ref int id, ref Pipeline.IChannelState channelItem);
        void GetChannelState<ConduitT>(ref bool IsOpen, ref bool IsChanneling);
        bool IsOpen { get; }
    }
}
