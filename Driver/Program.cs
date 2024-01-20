// See https://aka.ms/new-console-template for more information
using PipelineChunker;
using System.Collections;
using System.Data;
using System.Diagnostics;


static class Program {
    static void Main(string[] args) {
        var pipe = new Pipeline();

        var TestConduit1Open = false;
        var TestConduit1Channeling = false;
        do {
            for (int i = 0; i < 100; i++) {
                pipe.Channel<TestConduit1>(
                    Origin: () => {
                        return Pipelined1(i, pipe);
                    },
                    Operation: (conduit) => {
                        Debug.WriteLine(conduit);
                        // invoked when all enumerators terminate.
                    });
            }
            pipe.Flush<TestConduit1>();
            pipe.GetChannelState<TestConduit1>(ref TestConduit1Open, ref TestConduit1Channeling);
        } while (pipe.IsOpen);
    }


    internal class TestConduit1 : IConduit {
        int _id;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState channelItem1 => _channelItem;
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
        public class Phase1 : IPhase {
            public DataTable DataTable {
                get {
                    var table = new DataTable();
                    table.Columns.Add("SomeParameter");
                    return table;
                }
            }

            public void Execute(DataTable table) {
                throw new NotImplementedException();
            }
        }
        public class Phase2 : IPhase {
            public DataTable DataTable => throw new NotImplementedException();

            public void Execute(DataTable table) {
                throw new NotImplementedException();
            }
        }
    }
    internal class TestConduit2 : IConduit {
        int _id;
        int IConduit.Id => _id;
        Pipeline.IChannelState _channelItem;
        public Pipeline.IChannelState channelItem1 => _channelItem;
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

    static IEnumerable<TestConduit1> Pipelined1(int i, Pipeline owner) {
        var conduit = new TestConduit1();
        // first yield will initialize the conduit.
        yield return conduit;
        Debug.WriteLine($"fist {i}");
        conduit.channelItem1.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["SomeParameter"] = 1;
            },
            (DataTable table) => {

            });
        yield return conduit;

        conduit.channelItem1.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["SomeParameter"] = 1;
            },
            (DataTable table) => {

            });
        Debug.WriteLine($"second {i}");
        yield return conduit;

        conduit.channelItem1.Chunk<TestConduit1.Phase1>(
            conduit,
            (DataRow row) => {
                row["SomeParameter"] = 1;
            },
            (DataTable table) => {

            });
        Debug.WriteLine($"third {i}");
        yield return conduit;
    }
    static IEnumerable<TestConduit2> Pipelined2(int i, Pipeline owner) {
        var conduit = new TestConduit2();
        // first yield will initialize the conduit.
        yield return conduit;

        yield return conduit;
    }

}