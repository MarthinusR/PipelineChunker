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
                throw new Exception($"Bind<{typeof(ConduitT).FullName}>() must only be called from {typeof(IConduit<ConduitT>).FullName}'s {nameof(IConduit<ConduitT>.Initialize)} method");
            }
            return (ChannelState<ConduitT>)state;
        }

        public void Channel<ConduitT>(
            Action<ConduitT> Initializer = null,
            Func<int, IEnumerable<ConduitT>> Enumerator = null, Action<int, ConduitT> Operation = null
        ) where ConduitT : IConduit<ConduitT>, new() {
            var conduitType = typeof(ConduitT);
            ChannelState<ConduitT> typedState = null;
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                _conduitMap[conduitType] = state = typedState = new ChannelState<ConduitT>(this);
            }
            typedState = (ChannelState<ConduitT>)state;
            int indexCaptured = typedState.list.Count;
            ChannelItem<ConduitT> item = new ChannelItem<ConduitT>() {
                Operation = (IConduit<ConduitT> c) => {
                    try {
                        Operation?.Invoke(indexCaptured, (ConduitT)c);
                    } catch (Exception ex) {
                        var value = typedState.list[indexCaptured];
                        value.Exception = ex;
                        typedState.list[indexCaptured] = value;
                    }
                },
            };
            IEnumerator<IConduit<ConduitT>> enumerator = null;
            Exception exception = null;
            try {
                ConduitT conduit;
                if (Enumerator == null) {
                    conduit = new ConduitT();
                    Initializer?.Invoke(conduit);
                    enumerator = (IEnumerator<IConduit<ConduitT>>)conduit.GetEnumerator();
                } else {
                    enumerator = (IEnumerator<IConduit<ConduitT>>)Enumerator(indexCaptured).GetEnumerator();
                }
            }catch(Exception ex) {
                exception = ex;
            }
            item.Enumerator = enumerator;
            item.Exception = exception;

            typedState.list.Add(item);
        }

        ChannelState<ConduitT> InitChanelState<ConduitT>() where ConduitT : IConduit<ConduitT>, new() {
            ChannelState<ConduitT> state = new ChannelState<ConduitT>(this);
            _conduitMap[typeof(ConduitT)] = state;
            state.list = new List<ChannelItem<ConduitT>>();
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

        public void Flush<ConduitT>(out IChannelState outState, out IEnumerable<ConduitT> passed, out IEnumerable<ErrorConduit> failed) {
            outState = null;
            List<ConduitT> passedList = new List<ConduitT>();
            passed = passedList;
            var failedList = new List<ErrorConduit>();
            failed = failedList;

            var conduitType = typeof(ConduitT);
            Stopwatch stopwatch = new Stopwatch();

            if (_conduitMap.TryGetValue(conduitType, out var state)) {
                ChannelState<ConduitT> typedState = state as ChannelState<ConduitT>;
                typedState.IsChanneling = true;
                int chunkIndex = -1;
                while (chunkIndex * MaxChunkSize < typedState.list.Count) {
                    chunkIndex++;

                    stopwatch.Restart();
                    // Initialization
                    for (int i = chunkIndex * MaxChunkSize; i < typedState.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        var item = typedState.list[i];
                        try {
                            if (item.Exception != null)
                                continue;
                            if (!item.Enumerator.MoveNext() && item.Enumerator.Current == null) {
                                item.Exception = new Exception("Channel must receive a conduit that yields a non null item");
                                typedState.list[i] = item;
                            } else {
                                item.Enumerator.Current.Initialize(i, this);
                                if (item.Enumerator.Current.Id != i) {
                                    item.Exception = new Exception($"Channel must initialize '{typeof(int).Name} {nameof(IConduit<ConduitT>.Id)}' by assigning the id parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked");
                                }
                                if (item.Enumerator.Current.ChannelItem == null) {
                                    item.Exception = new Exception($"Channel must initialize '{nameof(Pipeline.IChannelState)} {nameof(IConduit<ConduitT>.ChannelItem)}' by assigning the conduitOwner parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked");
                                }
                            }
                        } catch (Exception ex) {
                            var value = item;
                            value.Exception = new Exception("Channel must receive a conduit that yields a non null value", ex);
                            typedState.list[i] = value;
                        }
                    }
                    stopwatch.Stop();
                    typedState.verticalTicks += stopwatch.ElapsedTicks;
                    typedState.IsChanneling = true;
                    do {
                        stopwatch.Restart();
                        // Iteration
                        for (int i = chunkIndex * MaxChunkSize; i < typedState.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                            var item = typedState.list[i];
                            if (item.Exception != null)
                                continue;
                            try {
                                var end = item.Enumerator.MoveNext();
                                typedState.IsChanneling &= !end;
                            } catch (Exception ex) {
                                item.Exception = ex;
                                typedState.list[i] = item;
                            }
                        }
                        stopwatch.Stop();
                        typedState.verticalTicks += stopwatch.ElapsedTicks;
                        stopwatch.Restart();
                        typedState.Execute();
                        stopwatch.Stop();
                        typedState.horizontalTicks += stopwatch.ElapsedTicks;
                        typedState.IsChanneling = !typedState.IsChanneling;
                    } while (state.IsChanneling);
                    // all iterators completed, run the operations for all conduits, but only once.
                    stopwatch.Restart();
                    for (int i = chunkIndex * MaxChunkSize; i < typedState.list.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        var item = typedState.list[i];
                        if (item.Exception != null) {
                            failedList.Add(new ErrorConduit(i, item.Exception));
                            continue;
                        }
                        try {
                            item.Operation(item.Enumerator.Current);
                            item = typedState.list[i];
                            if (item.Exception == null && item.Enumerator != null && item.Enumerator.Current != null)
                                passedList.Add((ConduitT)item.Enumerator.Current);
                            else
                                failedList.Add(new ErrorConduit(i, item.Exception ?? new Exception("Critical error [ba02c0dce4c040fbbfaae0478237896c]")));

                        } catch (Exception ex) {
                            item.Exception = ex;
                            typedState.list[i] = item;
                            failedList.Add(new ErrorConduit(i, item.Exception));
                        }
                    }
                    stopwatch.Stop();
                    typedState.verticalTicks += stopwatch.ElapsedTicks;
                    
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
