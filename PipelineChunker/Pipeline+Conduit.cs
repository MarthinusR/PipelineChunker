using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using static PipelineChunker.Pipeline;

namespace PipelineChunker {
    public partial class Pipeline {
        public abstract class Conduit<ConduitT> : IConduit<ConduitT> where ConduitT : Conduit<ConduitT>, new() {
            public abstract IEnumerator<Conduit<ConduitT>> GetEnumerator();
            public int ConduitId => (this as IConduit<ConduitT>).ChannelId;
            /// <summary>
            /// Collects units of operation into a chunk that will be executed in one shot.
            /// </summary>
            /// <typeparam name="StaticT"></typeparam>
            /// <typeparam name="InT"></typeparam>
            /// <typeparam name="OutT"></typeparam>/
            /// <typeparam name="DataT">
            /// <param name="ChunkInitializer">
            /// A _STATIC_ action that will be invoked once for each chunk in the channel.
            /// The value returned will be passed to <c>ConduitInitializer</c>, <c>ChunkTransform</c>, and <c>ConduitOperation</c>.
            /// </param>
            /// The type that will be passed around in the respective delegates.
            /// </typeparam>
            /// <param name="ConduitInitializer">
            /// Called per instance of the conduit.
            /// The returned value will be passed to <c>ChunkTransform</c> when the chunk gets flushed
            /// </param>
            /// <param name="ChunkTransform">
            /// A _STATIC_ action that receives a key value pair enumeration.
            /// Will be invoked once for each chunk in the channel.
            /// The value returned from <c>ConduitInitializer</c> is paired as the value and the conduit as the key
            /// in the key value pair respectfully.
            /// </param>
            /// <param name="ConduitOperation">
            /// An action that is invoked after <c>ChunkTransform</c> is complete.
            /// Its parameter will have the value transformed by <c>ChunkTransform</c>
            /// </param>
            /// <returns>Returns the associated conduit</returns>
            /// <returns></returns>
            protected ConduitT Chunk<StaticT, InT, OutT>(
                ChunkType<ConduitT, StaticT, InT, OutT>.ChunkInitializer ChunkInitializer,
                ChunkType<ConduitT, StaticT, InT, OutT>.ConduitInitializer ConduitInitializer,
                ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform ChunkTransform,
                ChunkType<ConduitT, StaticT, InT, OutT>.ConduitOperation ConduitOperation,
                string Name = null
            ) {
                (this as IConduit<ConduitT>).Channel.Chunk(
                    this,
                    ChunkInitializer: ChunkInitializer,
                    ConduitInitializer: ConduitInitializer,
                    ChunkTransform: ChunkTransform,
                    ConduitOperation: ConduitOperation,
                    Name: Name
                    );
                return (ConduitT)this;
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return ((Conduit<ConduitT>)this).GetEnumerator();
            }

            ChannelClass<ConduitT> IConduit<ConduitT>.Channel { get; set; }
            int IConduit<ConduitT>.ChannelId { get; set; }
        }
    }
}
