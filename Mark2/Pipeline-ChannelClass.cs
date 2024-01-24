using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        private abstract class ChannelAbstract {
            protected ChannelAbstract(Pipeline pipeline) { Pipeline = pipeline; }
            public Pipeline Pipeline { get; private set; }
        }
        private class ChannelClass<ConduitT> : ChannelAbstract, IChanel<ConduitT> where ConduitT : IConduit<ConduitT>, new() {
            private readonly Pipeline _pipeline;
            private int total = 0;
            private readonly ConduitWrapper[] chunk;


            public string Name { get; private set; }

            public ChannelClass(Pipeline pipeline) : base(pipeline) {
                _pipeline = pipeline;
                chunk = new ConduitWrapper[_pipeline._maxChunkSize];
            }

            public ConduitT Chunk<StaticT, InT, OutT>(
                Func<IChanel<ConduitT>, StaticT> ChunkInitializer,
                Func<IChanel<ConduitT>, StaticT, InT> ConduitInitializer,
                Func<IChanel<ConduitT>, StaticT, IEnumerable<KeyValuePair<ConduitT, InT>>, IEnumerable<KeyValuePair<ConduitT, OutT>>> ChunkTransform,
                Action<IChanel<ConduitT>, StaticT, KeyValuePair<ConduitT, OutT>> ConduitOperation,
                string Name = null
            ) {
                this.Name = Name ?? typeof(ConduitT).FullName;
                throw new NotImplementedException();
            }

            public void AddConduit(Action<ConduitT> channelInitializer, Action<ConduitT> channelFinalizer) {
                if (total >= Pipeline._maxChunkSize)
                    Flush();

                ConduitT conduit = new ConduitT();
                channelInitializer?.Invoke(conduit);
                chunk[total].conduit = conduit;
                chunk[total].channelFinalizer = channelFinalizer;
                total++;
            }

            public void Flush() {

                Reset();
            }

            private void Reset() {
                total = 0;
            }

            private struct ConduitWrapper {
                public ConduitT conduit;
                public Action<ConduitT> channelFinalizer;
            }

        }
    }
}
