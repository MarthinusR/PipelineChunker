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
        var pipe = new Pipeline();
        var connectionStringBuilder = new SqlConnectionStringBuilder();
        connectionStringBuilder.ConnectionString = "Server=localhost\\SQLEXPRESS;Database=PipelineChunker;Trusted_Connection=True;Encrypt=False;";
        Utilities.Init(connectionStringBuilder);

        using (SqlConnection conn = new SqlConnection(connectionStringBuilder.ConnectionString))
        using (SqlCommand cmd = conn.CreateCommand()) {
            conn.Open();
            var TestConduit1Open = false;
            var TestConduit1Channeling = false;
            do {
                for (int i = 0; i < 1000; i++) {
                    pipe.Channel<TestConduit1>(
                        Origin: () => {
                            return Pipelined1(cmd, i+1, pipe);
                        },
                        Operation: (conduit) => {
                            Debug.WriteLine($"Final sum: {conduit.sum} sumAbs: {conduit.sumAbs}");
                        });
                }
                pipe.Flush<TestConduit1>(out var state);
                Debug.WriteLine($"Vertical: {state.VerticalSeconds}, Horizontal: {state.HorizontalSeconds}");
                pipe.GetChannelState<TestConduit1>(ref TestConduit1Open, ref TestConduit1Channeling);
            } while (pipe.IsOpen);
        }
    }

    internal class TestConduit1 : IConduit {
        int _id;
        public int sum = 0;
        public int sumAbs = 0;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState channelItem => _channelItem;
        public IEnumerator<TestConduit1> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit.Initialize(IPipeline conduitOwner) {
            TestConduit1 self = this;
            conduitOwner.Bind(ref self, ref _id, ref _channelItem);
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
        public Pipeline.IChannelState channelItem => _channelItem;
        public IEnumerator<TestConduit2> GetEnumerator() {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }
        void IConduit.Initialize(IPipeline conduitOwner) {
            TestConduit2 self = this;
            conduitOwner.Bind(ref self, ref _id, ref _channelItem);
        }
    }

    static IEnumerable<TestConduit1> Pipelined1(SqlCommand cmd, int i, Pipeline owner) {
        var conduit = new TestConduit1();
        // first yield will initialize the conduit.
        yield return conduit;
        int someValue = 0;
        var phase1 = conduit.channelItem.Chunk<TestConduit1.Phase1>(
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

        Debug.WriteLine($"Phase1-computed: {someValue}  -- {i}");
        conduit.sum += someValue;
        conduit.sumAbs += Math.Abs(someValue);

        conduit.channelItem.Chunk<TestConduit1.Phase2>(
            conduit,
            (DataRow row) => {
                row["@a"] = -i;
                row["@b"] = -i;
            },
            (DataTable table, bool isError) => {
                someValue = (int)table.Rows[0].ItemArray[0];
            });
        yield return conduit;

        Debug.WriteLine($"Phase2-computed: {someValue}  -- {i}");
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