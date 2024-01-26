using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public interface IChanel {
            Pipeline Pipeline { get; }
            string Name { get; }
        }

        public interface IChanel<ConduitT> : IChanel where ConduitT: IConduit<ConduitT> {
            /// <summary>
            /// Collects units of operation into a chunk that will be executed in one shot.
            /// </summary>
            /// <typeparam name="StaticT"></typeparam>
            /// <typeparam name="InT"></typeparam>
            /// <typeparam name="OutT"></typeparam>/
            /// <typeparam name="DataT">
            /// <param name="ChunkInitializer">
            /// Will be invoked once for each chunk in the channel.
            /// The value returned from will be passed to <c>ConduitInitializer</c>, <c>ChunkTransform</c>, and <c>ConduitOperation</c>.
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
            ConduitT Chunk<StaticT, InT, OutT>(
                Func    <IChanel<ConduitT>, StaticT> ChunkInitializer,
                Func    <StaticT, InT> ConduitInitializer,
                Func    <IChanel<ConduitT>, StaticT, Pair<ConduitT, InT>[], Pair<ConduitT, OutT>[]> ChunkTransform,
                Action  <StaticT, Pair<ConduitT, OutT>> ConduitOperation,
                string Name = null
            );
        }
    }
}
