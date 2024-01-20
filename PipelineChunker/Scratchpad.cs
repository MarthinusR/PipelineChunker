using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using static System.Collections.Specialized.BitVector32;
using System.Linq;
using System.Diagnostics;

namespace PipelineChunker {
        public interface IPipeline {
            void Bind<ConduitT>(ref ConduitT conduitT, ref int id, ref Pipeline.IChannelState channelItem);
            void GetChannelState<ConduitT>(ref bool IsOpen, ref bool IsChanneling);
            bool IsOpen { get; }
        }
        public interface IConduit : IEnumerable {
            void Initialize(IPipeline conduitOwner);
            int Id { get; }
            Pipeline.IChannelState channelItem1 { get; }
        }
        public interface IPhase {
            DataTable DataTable { get; }
            void Execute(DataTable table);
        }

        public class Pipeline : IPipeline {

            private class ChannelState : IChannelState {
                public bool IsChanneling;
                public bool IsOpen;
                public List<ChannelItem> list;

                public void Chunk<PhaseT>(IConduit conduit, Action<DataRow> value, Action<DataTable> value1) {
                }
            }
            public interface IChannelState {
                void Chunk<PhaseT>(IConduit conduit, Action<DataRow> value, Action<DataTable> value1);
            }
            private struct ChannelItem {
                public Action<IConduit> Operation;
                public IEnumerator<IConduit> Enumerator;
            }
            private Dictionary<Type, ChannelState> _conduitMap = new Dictionary<Type, ChannelState>();

            public bool IsOpen {
                get {
                    return !_conduitMap.Values.All(x => !x.IsOpen);
                }
            }

            public void Bind<ConduitT>(ref ConduitT conduitT, ref int id, ref Pipeline.IChannelState channelItem) {
                var conduitType = typeof(ConduitT);
                if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                    throw new Exception($"Bind<{typeof(ConduitT).FullName}>() must only be called from {typeof(IConduit).FullName}'s {nameof(IConduit.Initialize)} method");
                }
                channelItem = state;
                id = state.list.Count - 1;
              }

            public void Channel<IConduitT>(Func<IEnumerable<IConduit>> Origin, Action<IConduit> Operation) {
                var conduitType = typeof(IConduitT);
                if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                    _conduitMap[conduitType] = state = new ChannelState();
                    state.list = new List<ChannelItem>();
                    state.IsOpen = false;
                    var enumerator = 
                    state.IsChanneling = false;
                }
                state.list.Add(new ChannelItem() {
                    Operation = Operation,
                    Enumerator = Origin().GetEnumerator()
                });
            }

            public void Clear() {
                //throw new NotImplementedException();
            }

            public IEnumerable<IEnumerator<IConduit>> Flush<IConduitT>() {
                var conduitType = typeof(IConduitT);
                if (_conduitMap.TryGetValue(conduitType, out var state)) {
                    bool wasChanneling = state.IsChanneling;
                    if (!state.IsOpen) {
                        foreach (var item in state.list) {
                            if (!item.Enumerator.MoveNext()) {
                                throw new Exception("Channel must receive a conduit that yields a non null value");
                            }
                            item.Enumerator.Current.Initialize(this);
                        }
                        state.IsChanneling = true;
                    }
                    while (state.IsChanneling) {
                        foreach (var item in state.list) {
                            var end = item.Enumerator.MoveNext();
                            state.IsChanneling &= !end;
                        }
                        state.IsChanneling = !state.IsChanneling;
                    }
                    // all iterators completed, run the operations for all conduits, but only once.
                    foreach (var item in state.list) {
                        item.Operation(item.Enumerator.Current);

                    }
                }
                return state.list.Select(x => x.Enumerator);
            }

            public void GetChannelState<ConduitT>(ref bool IsOpen, ref bool IsChanneling) {
                if (_conduitMap.TryGetValue(typeof(ConduitT), out var item)) {
                    IsOpen = item.IsOpen;
                    IsChanneling = item.IsChanneling;
                } else {
                    IsOpen = false;
                    IsChanneling = false;
                }
            }

        }
}
