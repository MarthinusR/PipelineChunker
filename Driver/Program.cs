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
        var pipe = new Pipeline(512);
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
                for (int i = 0; i < 10000; i++) {
                    pipe.Channel<TestConduit1>(
                        Origin: (id) => {
                            //if (id == 4)
                            //    throw new Exception("d58263c9527f4e20831b2f1860165064");
                            return Pipelined1(cmd, i+1, pipe);
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
                pipe.Flush<TestConduit1>(out var state, out var passed, out var failed);
                foreach (var conduit in passed) {
                    sum += conduit.sum;
                    sumAbs += conduit.sumAbs;
                }
                foreach (var conduit in failed) {
                    Debug.WriteLine($"Failed[{conduit.Id}]: {conduit.Exception}");
                }
                watch.Stop();
                Debug.WriteLine($"sum: {sum}, sumAbs: {sumAbs}");
                Console.WriteLine($"sum: {sum}, sumAbs: {sumAbs}");
                Debug.WriteLine($"Vertical: {state.VerticalSeconds}, Horizontal: {state.HorizontalSeconds}");
                Console.WriteLine($"Vertical: {state.VerticalSeconds}, Horizontal: {state.HorizontalSeconds}");
                Console.WriteLine($"Actual: {watch.ElapsedTicks / (double)Stopwatch.Frequency}");
                Debug.WriteLine($"Actual: {watch.ElapsedTicks / (double)Stopwatch.Frequency}");
                pipe.GetChannelState<TestConduit1>(ref TestConduit1Open, ref TestConduit1Channeling);
            } while (pipe.IsOpen);
        }
    }

    internal class TestConduit1 : IConduit {
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
        void IConduit.Initialize(int id, IPipeline conduitOwner) {
            _id = id;
            _channelItem = conduitOwner.Bind<TestConduit1>();
        }
        public class Phase1 : Phase {
            public SqlCommand cmd;
            public int value;
            public Phase1() { }
            override public IEnumerable<KeyValuePair<string, DataTable>> parameterTables {
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

            override public DataSet Execute(IEnumerable<KeyValuePair<String, DataTable>> parameterTables) {
                return Utilities.ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value);
            }
        }
        public class Phase2 : Phase {
            public SqlCommand cmd;
            override public IEnumerable<KeyValuePair<string, DataTable>> parameterTables {
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

            public override DataSet Execute(IEnumerable<KeyValuePair<string, DataTable>> parameterTables) {
                return Utilities.ExecFlattenedStoreProcAsDataSetBatcher(cmd, "usp_Example", parameterTables.First().Value);
            }
        }
    }
    internal class TestConduit2 : IConduit {
        int _id;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState ChannelItem => _channelItem;

        public IEnumerator<TestConduit2> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit.Initialize(int id, IPipeline conduitOwner) {
            _id = id;
            _channelItem = conduitOwner.Bind<TestConduit1>();
        }
    }

    static IEnumerable<TestConduit1> Pipelined1(SqlCommand cmd, int i, Pipeline owner) {
        //if (i == 1)
        //    throw new Exception("Init test");
        var conduit = new TestConduit1();
        // first yield will initialize the conduit.
        yield return conduit;
        int someValue = 0;
        var phase1 = conduit.ChannelItem.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["@a"] = i;
                row["@b"] = i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
            });
        phase1.cmd = cmd;
        yield return conduit;
        //if (i == 3)
        //    throw new Exception("TEST after step");

        //Debug.WriteLine($"Phase1-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        conduit.sumAbs += Math.Abs(someValue);

        conduit.ChannelItem.Chunk<TestConduit1.Phase2>(
            conduit,
            (DataRow row) => {
                row["@a"] = -i;
                row["@b"] = -i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
                if (i % 200 == 0)
                    Console.WriteLine($"Phase2-computed: {someValue}  -- {i}");
            });
        yield return conduit;

        //Debug.WriteLine($"Phase2-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        conduit.sumAbs += Math.Abs(someValue);
        yield return conduit;
    }
    static IEnumerable<TestConduit2> Pipelined2(int i, Pipeline owner) {
        var conduit = new TestConduit2();
        // first yield will initialize the conduit.
        yield return conduit;

        yield return conduit;
    }

}