using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
        public Pipeline(int maxChunkSize = 1024) {
            _maxChunkSize = maxChunkSize;
            Clear();
        }
        public bool IsOpen {
            get {
                return !_conduitMap.Values.All(x => !x.IsOpen);
            }
        }

        public int MaxChunkSize => _maxChunkSize;

        public Pipeline.IChannelState Bind<ConduitT>() {
            var conduitType = typeof(ConduitT);
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                throw new Exception($"Bind<{typeof(ConduitT).FullName}>() must only be called from {typeof(IConduit).FullName}'s {nameof(IConduit.Initialize)} method");
            }
            return state;
        }

        public void Channel<IConduitT>(Func<int, IEnumerable<IConduit>> Origin, Action<int, IConduitT> Operation) {
            var conduitType = typeof(IConduitT);
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                state = InitChanelState<IConduitT>();
            }
            int indexCaptured = state.list.Count;
            ChannelItem item = new ChannelItem() {
                Operation = (IConduit c) => {
                    try {
                        Operation(indexCaptured, (IConduitT)c);
                    } catch (Exception ex) {
                        var value = state.list[indexCaptured];
                        value.Exception = ex;
                        state.list[indexCaptured] = value;
                    }
                },
            };
            IEnumerator<IConduit> enumerator1 = null;
            Exception exception = null;
            try {
                enumerator1 = Origin(indexCaptured).GetEnumerator();
            }catch(Exception ex) {
                exception = ex;
            }
            item.Enumerator = enumerator1;
            item.Exception = exception;
            state.list.Add(item);
        }

        ChannelState InitChanelState<ConduitT>() {
            ChannelState state;
            _conduitMap[typeof(ConduitT)] = state = new ChannelState();
            state.list = new List<ChannelItem>();
            state.IsOpen = false;
            var enumerator =
            state.IsChanneling = false;
            state.verticalTicks = 0;
            state.horizontalTicks = 0;
            return state;
        }
        public void Clear() {
            _conduitMap.Clear();
            //throw new NotImplementedException();
        }

        public void Flush<IConduitT>(out IChannelState outState, out IEnumerable<IConduitT> passed, out IEnumerable<ErrorConduit> failed) {
            outState = null;
            List<IConduitT> passedList = new List<IConduitT>();
            passed = passedList;
            var failedList = new List<ErrorConduit>();
            failed = failedList;

            var conduitType = typeof(IConduitT);
            Stopwatch stopwatch = new Stopwatch();

            if (_conduitMap.TryGetValue(conduitType, out var state)) {
                state.IsChanneling = true;
                int chunkIndex = -1;
                while (chunkIndex * MaxChunkSize < state.list.Count) {
                    chunkIndex++;

                    stopwatch.Restart();
                    // Initialization
                    for (int i = chunkIndex * MaxChunkSize; i < state.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        var item = state.list[i];
                        try {
                            if (item.Exception != null)
                                continue;
                            if (!item.Enumerator.MoveNext()) {
                                item.Exception = new Exception("Channel must receive a conduit that yields a non null item");
                                state.list[i] = item;
                            } else {
                                item.Enumerator.Current.Initialize(i, this);
                                if (item.Enumerator.Current.Id != i) {
                                    item.Exception = new Exception($"Channel must initialize '{typeof(int).Name} {nameof(IConduit.Id)}' by assigning the id parameter when {conduitType.FullName}.{nameof(IConduit.Initialize)} is invoked");
                                }
                                if (item.Enumerator.Current.ChannelItem == null) {
                                    item.Exception = new Exception($"Channel must initialize '{nameof(Pipeline.IChannelState)} {nameof(IConduit.ChannelItem)}' by assigning the conduitOwner parameter when {conduitType.FullName}.{nameof(IConduit.Initialize)} is invoked");
                                }
                            }
                        } catch (Exception ex) {
                            var value = item;
                            value.Exception = new Exception("Channel must receive a conduit that yields a non null value", ex);
                            state.list[i] = value;
                        }
                    }
                    stopwatch.Stop();
                    state.verticalTicks += stopwatch.ElapsedTicks;
                    state.IsChanneling = true;
                    do {
                        stopwatch.Restart();
                        // Iteration
                        for (int i = chunkIndex * MaxChunkSize; i < state.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                            var item = state.list[i];
                            if (item.Exception != null)
                                continue;
                            try {
                                var end = item.Enumerator.MoveNext();
                                state.IsChanneling &= !end;
                            } catch (Exception ex) {
                                item.Exception = ex;
                                state.list[i] = item;
                            }
                        }
                        stopwatch.Stop();
                        state.verticalTicks += stopwatch.ElapsedTicks;
                        stopwatch.Restart();
                        state.Execute();
                        stopwatch.Stop();
                        state.horizontalTicks += stopwatch.ElapsedTicks;
                        state.IsChanneling = !state.IsChanneling;
                    } while (state.IsChanneling);
                    // all iterators completed, run the operations for all conduits, but only once.
                    stopwatch.Restart();
                    for (int i = chunkIndex * MaxChunkSize; i < state.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        var item = state.list[i];
                        if (item.Exception != null) {
                            failedList.Add(new ErrorConduit(i, item.Exception));
                            continue;
                        }
                        try {
                            item.Operation(item.Enumerator.Current);
                            item = state.list[i];
                            if (item.Exception == null && item.Enumerator != null && item.Enumerator.Current != null)
                                passedList.Add((IConduitT)item.Enumerator.Current);
                            else
                                failedList.Add(new ErrorConduit(i, item.Exception ?? new Exception("Critical error [ba02c0dce4c040fbbfaae0478237896c]")));

                        } catch (Exception ex) {
                            item.Exception = ex;
                            state.list[i] = item;
                            failedList.Add(new ErrorConduit(i, item.Exception));
                        }
                    }
                    stopwatch.Stop();
                    state.verticalTicks += stopwatch.ElapsedTicks;
                    
                }
            }
            outState = state;
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
