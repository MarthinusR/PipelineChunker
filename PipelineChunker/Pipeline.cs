using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
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

        public void Channel<IConduitT>(Func<IEnumerable<IConduit>> Origin, Action<IConduitT> Operation) {
            var conduitType = typeof(IConduitT);
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                _conduitMap[conduitType] = state = new ChannelState();
                state.list = new List<ChannelItem>();
                state.IsOpen = false;
                var enumerator =
                state.IsChanneling = false;
                state.verticalTicks = 0;
                state.horizontalTicks = 0;
            }
            state.list.Add(new ChannelItem() {
                Operation = (IConduit c) => Operation((IConduitT)c),
                Enumerator = Origin().GetEnumerator()
            });
        }

        public void Clear() {
            //throw new NotImplementedException();
        }

        public IEnumerable<IEnumerator<IConduit>> Flush<IConduitT>(out IChannelState outState) {
            outState = null;
            var conduitType = typeof(IConduitT);
            Stopwatch stopwatch = new Stopwatch();
            if (_conduitMap.TryGetValue(conduitType, out var state)) {
                bool wasChanneling = state.IsChanneling;
                if (!state.IsOpen) {
                    stopwatch.Restart();
                    foreach (var item in state.list) {
                        if (!item.Enumerator.MoveNext()) {
                            throw new Exception("Channel must receive a conduit that yields a non null value");
                        }
                        item.Enumerator.Current.Initialize(this);
                    }
                    stopwatch.Stop();
                    state.verticalTicks += stopwatch.ElapsedTicks;
                    state.IsChanneling = true;
                }
                while (state.IsChanneling) {
                    stopwatch.Restart();
                    foreach (var item in state.list) {
                        var end = item.Enumerator.MoveNext();
                        state.IsChanneling &= !end;
                    }
                    stopwatch.Stop();
                    state.verticalTicks += stopwatch.ElapsedTicks;
                    stopwatch.Restart();
                    state.Execute();
                    stopwatch.Stop();
                    state.horizontalTicks += stopwatch.ElapsedTicks;
                    state.IsChanneling = !state.IsChanneling;
                }
                // all iterators completed, run the operations for all conduits, but only once.
                stopwatch.Restart();
                foreach (var item in state.list) {
                    item.Operation(item.Enumerator.Current);
                }
                stopwatch.Stop();
                state.verticalTicks += stopwatch.ElapsedTicks;
            }
            outState = state;
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
