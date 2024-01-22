// See https://aka.ms/new-console-template for more information
using PipelineChunker;
using System.Collections;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Data.SqlTypes;
using Driver;

static class Program {    
    static void Main(string[] args) {
        // NOTE: optimization gain is based on MaxChunkSize that is positively correlated with network latency
        //       I.e. use larger values where the network response (ping) is slow (values between 256 and 1024)
        //       If the server is located on an internal network then use in the rage of 32 to 256
        var pipe = new Pipeline(7);
        var connectionStringBuilder = new SqlConnectionStringBuilder();
        connectionStringBuilder.ConnectionString = "Server=localhost\\SQLEXPRESS;Database=PipelineChunker;Trusted_Connection=True;Encrypt=False;";
        Utilities.Init(connectionStringBuilder);

        using (SqlConnection conn = new SqlConnection(connectionStringBuilder.ConnectionString))
        using (SqlCommand cmd = conn.CreateCommand()) {
            conn.Open();
            var TestConduit1Open = false;
            var TestConduit1Channeling = false;
            var watch = new Stopwatch();
            watch.Start();
            do {
                for (int i = 0; i < 10; i++) {
                    pipe.Channel<TestConduit1>(
                        Enumerator: (id) => {
                            //if (id == 4)
                            //    throw new Exception("d58263c9527f4e20831b2f1860165064");
                            return Pipelined1(cmd, i+1);
                        },
                        Operation: (id, conduit) => {
                            //if(id == 1)
                            //    throw new Exception("f7d4e71726eb4fadaa55b975d015dd57");
                            //Debug.WriteLine($"Final sum: {conduit.sum} sumAbs: {conduit.sumAbs} -- {id}");
                            //Console.WriteLine($"Final sum: {conduit.sum} sumAbs: {conduit.sumAbs} -- {id}");
                        });
                }
                int sum = 0;
                int sumAbs = 0;
                int check = 0;
                int value = 0;
                pipe.Flush<TestConduit1>(out var state, out var passed, out var failed);
                foreach (var conduit in passed) {
                    value++;
                    sum += conduit.sum;
                    sumAbs += conduit.sumAbs;
                    check += (value * 2) + (value + value) * 2 - value;
                }
                foreach (var conduit in failed) {
                    Debug.WriteLine($"Failed[{conduit.Id}]: {conduit.Exception}");
                }
                watch.Stop();
                Debug.WriteLine($"sum: {sum}, sumAbs: {sumAbs} [{check == sum}]");
                Console.WriteLine($"sum: {sum}, sumAbs: {sumAbs} [{check == sum}]");
                Debug.WriteLine($"Vertical: {state.VerticalSeconds}, Horizontal: {state.HorizontalSeconds}");
                Console.WriteLine($"Vertical: {state.VerticalSeconds}, Horizontal: {state.HorizontalSeconds}");
                Console.WriteLine($"Actual: {watch.ElapsedTicks / (double)Stopwatch.Frequency}");
                Debug.WriteLine($"Actual: {watch.ElapsedTicks / (double)Stopwatch.Frequency}");
                pipe.GetChannelState<TestConduit1>(ref TestConduit1Open, ref TestConduit1Channeling);
            } while (pipe.IsOpen);
        }
    }

    internal class TestConduit1 : IConduit<TestConduit1> {
        int _id;
        public int sum = 0;
        public int sumAbs = 0;
        public int Id => _id;

        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState ChannelItem => _channelItem;

        public IEnumerator<TestConduit1> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit<TestConduit1>.Initialize(int id, IPipeline conduitOwner) {
            _id = id;
            _channelItem = conduitOwner.Bind<TestConduit1>();
        }
        public class Phase1 : Phase {
            public SqlCommand cmd;
            public int value;
            override public IEnumerable<KeyValuePair<string, DataTable>> ParameterTables {
                get {
                    var table = new DataTable();
                    table.Columns.Add("@a");
                    table.Columns.Add("@b");
                    var theParameterTables = new KeyValuePair<string, DataTable>[]{
                        new KeyValuePair<string, DataTable>("@input", table)
                    };
                    return theParameterTables;
                }
            }
            override public DataSet Collect(Pipeline.IChannelState channelState, IEnumerable<KeyValuePair<String, DataTable>> parameterTables) {
                return Utilities.ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value);
            }
        }
        public class Phase2 : Phase {
            public SqlCommand cmd;
            override public IEnumerable<KeyValuePair<string, DataTable>> ParameterTables {
                get {
                    var table = new DataTable();
                    table.Columns.Add("@a");
                    table.Columns.Add("@b");
                    var theParameterTables = new KeyValuePair<string, DataTable>[]{
                        new KeyValuePair<string, DataTable>("@input", table)
                    };
                    return theParameterTables;
                }
            }

            public override DataSet Collect(Pipeline.IChannelState channelState, IEnumerable<KeyValuePair<string, DataTable>> parameterTables) {
                channelState.Pipeline.Flush<TestConduit2>(out var state, out var passed, out var failed);
                var table = parameterTables.First().Value;
                int i = -1;
                foreach (var item in passed) {
                    i++;
                    table.Rows[i]["@a"] = item.testValue;//<-- (i + i) [1]
                    table.Rows[i]["@b"] = table.Rows[i]["@b"];//<-- -i [2]
                    //.-- (i + i) * 2 - i [3]
                    Debug.WriteLine($"TestConduit1-Phase2-Flush-TestConduit2-passed: @a:{table.Rows[i]["@a"]}, @b:{table.Rows[i]["@b"]} testValue:{item.testValue} -- id:{i}");
                }
                foreach (var item in failed) {
                    Debug.WriteLine($"TestConduit1-Phase2-Flush-TestConduit2-failed: {item.Exception} -- id:{i}");
                }
                return Utilities.ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value); 
            }
        }
    }
    internal class TestConduit2 : IConduit<TestConduit2> {
        int _id;
        int IConduit<TestConduit2>.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState ChannelItem => _channelItem;

        public int testValue;

        void IConduit<TestConduit2>.Initialize(int id, IPipeline conduitOwner) {
            _id = id;
            _channelItem = conduitOwner.Bind<TestConduit1>();
        }
        public IEnumerator GetEnumerator() {
            return ((IEnumerable<TestConduit2>)this).GetEnumerator();
        }

        IEnumerator<TestConduit2> IEnumerable<TestConduit2>.GetEnumerator() {
            yield return this;// <-- Init
            testValue *= 2;
            Debug.WriteLine($"TestConduit2 {testValue} -- {_id + 1}");
            yield return this;
        }
    }

    static IEnumerable<TestConduit1> Pipelined1(SqlCommand cmd, int i) {
        //if (i == 1)
        //    throw new Exception("Init test");
        var conduit = new TestConduit1();
        // first yield will initialize the conduit.
        yield return conduit;
        int someValue = 0;
        Debug.WriteLine($"TestConduit1-Phase1-PreChunk {someValue} -- {i}");
        var phase1 = conduit.ChannelItem.Chunk<TestConduit1, TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["@a"] = i;
                row["@b"] = i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0]; //<-- i + i [1]
                Debug.WriteLine($"TestConduit1-Phase1-Operation {someValue} -- {i}");
            });
        phase1.cmd = cmd;
        Debug.WriteLine($"TestConduit1-Phase1-PostChunk {someValue} -- {i}");
        yield return conduit;
        Debug.WriteLine($"TestConduit1-Phase2-PreChunk {someValue} -- {i}");


        conduit.ChannelItem.Pipeline.Channel<TestConduit2>(
            Initializer:(conduit) => {
                conduit.testValue = someValue;//<-- [1]
                Debug.WriteLine($"TestConduit2-Initializer {someValue} -- {i}");
            });

        //if (i == 3)
        //    throw new Exception("TEST after step");

        //Debug.WriteLine($"Phase1-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        conduit.sumAbs += Math.Abs(someValue);

        conduit.ChannelItem.Chunk<TestConduit1, TestConduit1.Phase2>(
            conduit,
            (DataRow row) => {
                row["@a"] = -i;//<-- -i [2]
                row["@b"] = -i;//<-- -i [2]
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
                //.-- (i + i) * 2 - i [3]
                Debug.WriteLine($"TestConduit1-Phase1-Operation {someValue} -- {i}");
            });
        Debug.WriteLine($"TestConduit1-Phase2-PostChunk {someValue} -- {i}");
        yield return conduit;
        Debug.WriteLine($"TestConduit1-End: should be check ({i} + {i}) * 2 - {i} = {someValue} [{(i + i) * 2 - i == someValue}] -- {i}");


        //Debug.WriteLine($"Phase2-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        conduit.sumAbs += Math.Abs(someValue);
        yield return conduit;
    }
}