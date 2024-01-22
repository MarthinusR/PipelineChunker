using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public interface IConduit<T> : IEnumerable<T> {
        void Initialize(int id, IPipeline conduitOwner);
        int Id { get; }
        Pipeline.IChannelState ChannelItem { get; }
    }
}
