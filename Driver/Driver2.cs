using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure;
using Mark2;

namespace Driver {
    internal class Driver2 {
        public static void TheMain(string[] args) {
            Pipeline pipeline = new Pipeline(2);
            int sum = 0;
            for (int i = 0; i < 10; i++) {
                pipeline.Chanel<MainConduit>(
                        Initializer: (conduit) => conduit.Setup(3 + i, 5 + 2 * i),
                        Finalizer: (conduit) => {
                            sum += i;
                            Debug.WriteLine($"{conduit.Id} A: {conduit.A} {conduit.B}");
                        }
                    );
            }

            for (int i = 0; i < 10; i++) {
                pipeline.Chanel<OtherConduit>(
                    Initializer: (conduit) => {

                    },
                    Finalizer: (conduit) => {

                    }
                );
            }
            for (int i = 0; i < 10; i++) {
                pipeline.Chanel<MainConduit>(
                        Initializer: (conduit) => conduit.Setup(3 - i, 5 + 2 * i),
                        Finalizer: (conduit) => {
                            sum += i;
                            Debug.WriteLine($"{conduit.Id} A: {conduit.A} {conduit.B}");
                        }
                    );
            }
            pipeline.Flush();
        }

        private class MainConduit : Pipeline.IConduit<MainConduit> {
            public int A { get; private set; }
            public int B { get; private set; }
            public void Setup(int a, int b) { A = a; B = b; }
            public IEnumerator<MainConduit> GetEnumerator() {
                yield return this;
                yield return Channel.Chunk<DataTable, DataRow, DataRow>(
                    ChunkInitializer: static (channel) => new DataTable(),
                    ConduitInitializer: (channel, dt) => {
                        channel.Pipeline.Chanel<OtherConduit>((other) => { }, (other) => { });
                        return dt.NewRow();
                    },
                    ChunkTransform: static (channel, dt, values) => {
                        channel.Pipeline.Flush();
                        return values;
                    },
                    ConduitOperation: (channel, dt, value) => {

                    }
                );


                //Channel.Pipeline.Flush(); // <--- What does this mean? <-- Should throw error correct?

                for (int i = 0; i < 3 - Id; i++) {
                    yield return Channel.Chunk<DataTable, DataRow, DataRow>(
                        ChunkInitializer: static (channel) => new DataTable(),
                        ConduitInitializer: (channel, dt) => {
                            channel.Pipeline.Chanel<OtherConduit>((other) => { }, (other) => { });
                            return dt.NewRow();
                        },
                        ChunkTransform: static (channel, dt, values) => {
                            channel.Pipeline.Flush();
                            return values;
                        },
                        ConduitOperation: (channel, dt, value) => {

                        }
                    );
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => (this as Pipeline.IConduit<MainConduit>).GetEnumerator();
            public int Id { get; private set; }
            public Pipeline.IChanel<MainConduit> Channel {get; private set;}
            public Exception Exception { get; private set; }
            public void Initialize(int id, Pipeline.IChanel<MainConduit> channel, out Action<Exception> SetException){
                Id = id; Channel = channel;
                SetException = (ex) => Exception = ex;
            }
        }
        private class OtherConduit : Pipeline.IConduit<OtherConduit> {
            public IEnumerator<OtherConduit> GetEnumerator() {
                yield return this;
                yield return Channel.Chunk<DataTable, DataRow, DataRow>(
                    ChunkInitializer: static (channel) => new DataTable(),
                    ConduitInitializer: (channel, dt) => {
                        channel.Pipeline.Chanel<OtherConduit>((other) => { }, (other) => { });
                        return dt.NewRow();
                    },
                    ChunkTransform: static (channel, dt, values) => {
                        return values;
                    },
                    ConduitOperation: (channel, dt, value) => {

                    }
                );
            }
            IEnumerator IEnumerable.GetEnumerator() => (this as Pipeline.IConduit<OtherConduit>).GetEnumerator();
            public int Id { get; private set; }
            public Pipeline.IChanel<OtherConduit> Channel { get; private set; }
            public Exception Exception { get; private set; }
            public void Initialize(int id, Pipeline.IChanel<OtherConduit> channel, out Action<Exception> SetException) {
                Id = id; Channel = channel;
                SetException = (ex) => Exception = ex;
            }
        }
    }
}
