using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public interface IConduit : IEnumerable {
        void Initialize(IPipeline conduitOwner);
        int Id { get; }
        Pipeline.IChannelState channelItem { get; }
    }
}
