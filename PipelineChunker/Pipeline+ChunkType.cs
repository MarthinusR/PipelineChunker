using System;
using System.Collections.Generic;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        public struct ChunkType<ConduitT, StaticT, InT, OutT> where ConduitT : Conduit<ConduitT>, new() {
            public delegate StaticT ChunkInitializer(IChannel<ConduitT> channel);
            public delegate InT ConduitInitializer(StaticT data);
            public delegate Item<ConduitT, OutT>[] ChunkTransform(IChannel<ConduitT> channel, StaticT data, Item<ConduitT, InT>[] input, int pristineTotal);
            public delegate void ConduitOperation(StaticT data, Item<ConduitT, OutT> item, Exception exception, ExceptionCommunicator communicator);
        }
    }
}
