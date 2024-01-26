using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Azure;
using Mark2;
using static Mark2.Pipeline;

namespace Driver {
    internal class Driver2 {
        public static void TheMain(string[] args) {
            Pipeline pipeline = new Pipeline(7);
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
            pipeline.Flush();
            return;
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
        }
        static void Other () => Debug.WriteLine($"Test Other");
        delegate void TestMethod();

        class TestAttribute : Attribute { }
        class Naughty { }
        private class MainConduit :  Pipeline.IConduit<MainConduit> {
            public int A { get; private set; }
            public int B { get; private set; }
            int term;
            int termTimes10;
            public void Setup(int a, int b) { A = a; B = b; }

            public IEnumerator<MainConduit> GetEnumerator() {
                Debug.WriteLine($"MainConduit[{Id}] - Start A:{A}, B:{B}");
                yield return this;
                yield return Channel.Chunk<Pipeline, int, int>(
                    ChunkInitializer: static (channel) => new Pipeline(4),
                    ConduitInitializer: (pipeline) => {
                        pipeline.Chanel<OtherConduit>(
                        (conduit) => conduit.Setup(A * 2, B * 4),
                        (conduit) => {
                            bool check = conduit.Sum == 2 * (1 + A * 2 + B * 4);
                            Debug.WriteLine("term = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)");
                            Debug.WriteLine($"1[{check}] MainConduit[{Id}] - OtherConduit[{conduit.Id}] - Finalizer OtherA:{conduit.OtherA}, OtherB:{conduit.OtherB}, term:{conduit.Sum}");
                            
                            term = conduit.Sum; // <-- conduit.Sum = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)
                        });
                        return 0;//NOP
                    },
                    ChunkTransform: static (channel, pipeline, values) => {
                        Debug.WriteLine($"MainConduit[?] - ChunkTransform -- Flush");
                        pipeline.Flush();
                        for(int i = 0; i < values.Length; i++) {
                            int value = values[i].Conduit.term;
                            //bool check = term == 2 * (1 + A * 2 + B * 4);
                            values[i].Value = value * 10; // <-- 10 * 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)
                            Debug.WriteLine("term = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)");
                            Debug.WriteLine($"[  ?  ] MainConduit[{values[i].Conduit.Id}] - ChunkTransform term:{value}");
                        }
                        return values;
                    },
                    ConduitOperation: (dt, pair) => {
                        termTimes10 = pair.Value;
                        bool check = pair.Value == 10 * term;
                        Debug.WriteLine($"2[{check}] MainConduit[{Id}] - ChunkTransform term:{pair.Value}");
                    });
                Debug.WriteLine($"MainConduit[{Id}] - Post Chunk A:{A}, B:{B}, term:{term}, termTimes10:{termTimes10}");
            }
            IEnumerator IEnumerable.GetEnumerator() => (this as Pipeline.IConduit<MainConduit>).GetEnumerator();
            public int Id { get; private set; }
            public Pipeline.IChanel<MainConduit>? Channel {get; private set;}
            public Exception? Exception { get; private set; }
            public void Initialize(int id, Pipeline.IChanel<MainConduit> channel, out Action<Exception> SetException){
                Id = id; Channel = channel;
                SetException = (ex) => Exception = ex;
            }
        }
        private class OtherConduit : Pipeline.IConduit<OtherConduit> {
            public IEnumerator<OtherConduit> GetEnumerator() {
                yield return this;
                yield return Channel.Chunk<int, int, int>(
                    ChunkInitializer: static (channel) => 1,
                    ConduitInitializer: (start) => {
                        Debug.WriteLine($"OtherConduit[{Id}] - ConduitInitializer  start:{start}, OtherA:{OtherA}, OtherB:{OtherB}");
                        return start + OtherA + OtherB;
                    },
                    ChunkTransform: static (channel, start, values) => {
                        for (int i = 0; i < values.Length; i++) {
                            Debug.WriteLine($"OtherConduit[{values[i].Conduit.Id}] - ConduitInitializer  start:{start}, OtherA:{values[i].Conduit.OtherA}, OtherB:{values[i].Conduit.OtherB}, value:{values[i].Value}");
                            values[i].Value *= 2;
                        }
                        return values;
                    },
                    ConduitOperation: (start, pair) => {
                        Debug.WriteLine($"OtherConduit[{pair.Conduit.Id}] - ConduitInitializer  start:{start}, Sum = value * 2:{pair.Value}");
                        Sum = pair.Value; // Sum = 2 * (1 + OtherA + OtherB);
                    });
            }
            IEnumerator IEnumerable.GetEnumerator() => (this as Pipeline.IConduit<OtherConduit>).GetEnumerator();
            public int Id { get; private set; }
            public Pipeline.IChanel<OtherConduit>? Channel { get; private set; }
            public Exception? Exception { get; private set; }
            public void Initialize(int id, Pipeline.IChanel<OtherConduit> channel, out Action<Exception> SetException) {
                Id = id; Channel = channel;
                SetException = (ex) => Exception = ex;
            }
            public int Sum { get; private set; }
            public int OtherA { get; private set; }
            public int OtherB { get; private set; }
            public void Setup(int a, int b) { OtherA = a; OtherB = b; }
        }
    }
}
