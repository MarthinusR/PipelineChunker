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
                        Finalizer: (conduit, ex, communicator) => {
                            sum += i;
                            Debug.WriteLine($"{conduit.ConduitId} A: {conduit.A} {conduit.B}");
                        }
                    );
            }
            pipeline.Flush();
            return;
            //for (int i = 0; i < 10; i++) {
            //    pipeline.Chanel<OtherConduit>(
            //        Initializer: (conduit) => {

            //        },
            //        Finalizer: (conduit) => {

            //        }
            //    );
            //}
            //for (int i = 0; i < 10; i++) {
            //    pipeline.Chanel<MainConduit>(
            //            Initializer: (conduit) => conduit.Setup(3 - i, 5 + 2 * i),
            //            Finalizer: (conduit) => {
            //                sum += i;
            //                Debug.WriteLine($"{conduit.ConduitId} A: {conduit.A} {conduit.B}");
            //            }
            //        );
            //}
        }
        static void Other () => Debug.WriteLine($"Test Other");
        delegate void TestMethod();

        class TestAttribute : Attribute { }
        class Naughty { }
        private class MainConduit :  Pipeline.Conduit<MainConduit>{
            static int IdCounter = 0;
            public int ConduitId { get; private set; } = IdCounter++;
            public int A { get; private set; }
            public int B { get; private set; }
            int term;
            int termTimes10;
            public void Setup(int a, int b) { A = a; B = b; }

            public override IEnumerator<MainConduit> GetEnumerator() {
                Debug.WriteLine($"MainConduit[{ConduitId}] - Start A:{A}, B:{B}");
                yield return Chunk<Pipeline, int, int>(
                    ChunkInitializer: static (channel) => new Pipeline(2),
                    ConduitInitializer: (pipeline) => {
                        pipeline.Chanel<OtherConduit>(
                        (conduit) => {
                            conduit.Setup(ConduitId, A * 2, B * 4); },
                        (conduit, ex, communicator) => {
                            if(ex != null) {
                                Debug.WriteLine(ex);
                                communicator.ExceptionHandled = true;
                            }
                            bool check = conduit.Sum == 2 * (1 + A * 2 + B * 4);
                            Debug.WriteLine("term = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)");
                            Debug.WriteLine($"1[{check}] MainConduit[{ConduitId}] - OtherConduit[{conduit.ConduitId}] - Finalizer OtherA:{conduit.OtherA}, OtherB:{conduit.OtherB}, term:{conduit.Sum}");
                            
                            term = conduit.Sum; // <-- conduit.Sum = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)
                        });
                        return 0;//NOP
                    },
                    ChunkTransform: static (channel, pipeline, values) => {
                        Debug.WriteLine($"MainConduit[?] - ChunkTransform -- Flush");
                        pipeline.Flush();
                        for(int i = 0; i < values.Length; i++) {
                            if (values[i].Exception != null)
                                Debug.WriteLine($"ChunkTransform Exception detected  for conduit id: {values[i].Conduit.ConduitId}");
                            int value = values[i].Conduit.term;
                            //bool check = term == 2 * (1 + A * 2 + B * 4);
                            values[i].Value = value * 10; // <-- 10 * 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)
                            Debug.WriteLine("term = 2 * (1 + OtherA + OtherB) = 2 * (1 + A * 2 + B * 4)");
                            Debug.WriteLine($"[  ?  ] MainConduit[{values[i].Conduit.ConduitId}] - ChunkTransform term:{value}");
                        }
                        return values;
                    },
                    ConduitOperation: (dt, pair, communicator) => {
                        termTimes10 = pair.Value;
                        bool check = pair.Value == 10 * term;
                        Debug.WriteLine($"2[{check}] MainConduit[{ConduitId}] - ChunkTransform term:{pair.Value}");
                    });
                Debug.WriteLine($"MainConduit[{ConduitId}] - Post Chunk A:{A}, B:{B}, term:{term}, termTimes10:{termTimes10}");
            }
        }
        private class OtherConduit : Pipeline.Conduit<OtherConduit> {
            static int IdCounter = 0;
            public readonly int ConduitId = IdCounter++;
            public override IEnumerator<OtherConduit> GetEnumerator() {
                yield return this;
                if (CallingConduitId == 1)
                    throw new Exception($"Debug Test 1 GetEnumerator().Next()| ConduitId:{ConduitId}, CallingConduitId:{CallingConduitId}");
                yield return Chunk<int, int, int>(
                    ChunkInitializer: static (channel) => 1,//throw new Exception("Debug test 2 : ChunkInitializer"),
                    ConduitInitializer: (start) => {
                        if (CallingConduitId == 3)
                            throw new Exception($"Debug Test 3 - ConduitInitializer | callingConduitId:{CallingConduitId}");
                        Debug.WriteLine($"OtherConduit[{ConduitId}] - ConduitInitializer  start:{start}, OtherA:{OtherA}, OtherB:{OtherB}");
                        return start + OtherA + OtherB;
                    },
                    ChunkTransform: static (channel, start, values) => {
                        for (int i = 0; i < values.Length; i++) {
                            Debug.WriteLine($"OtherConduit[{values[i].Conduit.ConduitId}] - ConduitInitializer  start:{start}, OtherA:{values[i].Conduit.OtherA}, OtherB:{values[i].Conduit.OtherB}, value:{values[i].Value}");
                            values[i].Value *= 2;
                        }
                        return values;
                    },
                    ConduitOperation: (start, pair, communicator) => {
                        if(CallingConduitId == 4)
                            new Exception($"Debug Test 4 - ConduitOperation | CallingConduitId:{CallingConduitId}");
                        Debug.WriteLine($"OtherConduit[{pair.Conduit.ConduitId}] - ConduitInitializer  start:{start}, Sum = value * 2:{pair.Value}");
                        Sum = pair.Value; // Sum = 2 * (1 + OtherA + OtherB);
                    });
            }
            public int CallingConduitId { get; private set; }
            public int Sum { get; private set; }
            public int OtherA { get; private set; }
            public int OtherB { get; private set; }
            public void Setup(int callingConduitId, int a, int b) { if (callingConduitId == 0) { throw new Exception($"Debug Test 0 - Setup | callingConduitId:{callingConduitId}"); } CallingConduitId = callingConduitId; OtherA = a; OtherB = b; }
        }
    }
}
