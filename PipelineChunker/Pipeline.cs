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
        private Pipeline(Pipeline parent, int maxChunkSize) : this(maxChunkSize) {
            Debug.WriteLine($"The New: ParentId: {parent._pipelineId} ThisId: {_pipelineId}");
            _parent = parent;
        }
        public bool IsOpen {
            get {
                return !_conduitMap.Values.All(x => !x.IsOpen);
            }
        }

        public int MaxChunkSize => _maxChunkSize;

        public IEnumerable<int> ValidIds => _conduitMap.SelectMany(x => x.Value.ValidIds);

        public Pipeline.IChannelState Bind<ConduitT>() {
            var conduitType = typeof(ConduitT);
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                throw new Exception($"Bind<{typeof(ConduitT).FullName}>() must only be called from {typeof(IConduit<ConduitT>).FullName}'s {nameof(IConduit<ConduitT>.Initialize)} method");
            }
            return (ChannelState<ConduitT>)state;
        }

        void MoveToErrorMap<ChannelStateT, T>(ChannelStateT typedState, int indexCaptured, Exception ex) where ChannelStateT:ChannelState<T> {
            //get as valid
            var value = typedState.itemValidMap[indexCaptured];
            //assign exception
            value.Exception = ex;
            //and add to error map
            typedState.itemErrorMap[indexCaptured] = value;
            //remove from valid map
            typedState.itemValidMap.Remove(indexCaptured);
        }

        public void Channel<ConduitT>(
            Action<ConduitT> Initializer = null,
            Func<int, IEnumerable<ConduitT>> Enumerator = null, Action<int, ConduitT> Operation = null
        ) where ConduitT : IConduit<ConduitT>, new() {
            var conduitType = typeof(ConduitT);
            ChannelState<ConduitT> typedState = null;
            if (!_conduitMap.TryGetValue(conduitType, out var state)) {
                Debug.WriteLine($"New {this._pipelineId}");
                _conduitMap[conduitType] = state = typedState = new ChannelState<ConduitT>(this);
            }
            typedState = (ChannelState<ConduitT>)state;
            int indexCaptured = typedState.GetNextAndStepValidId();
            ChannelItem<ConduitT> item = new ChannelItem<ConduitT>() {
                Operation = (IConduit<ConduitT> c) => {
                    try {
                        Operation?.Invoke(indexCaptured, (ConduitT)c);
                    } catch (Exception ex) {
                        MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, ex);
                    }
                }
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
            } catch (Exception ex) {
                exception = ex;
            }
            item.Enumerator = enumerator;
            item.Exception = exception;

            typedState.itemValidMap[indexCaptured] = item;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Restart();
            // Initialization
            try {
                if (item.Exception != null)
                    return;
                if (!item.Enumerator.MoveNext() && item.Enumerator.Current == null) {
                    MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, new ChannelInitializationException("Channel must receive a conduit that yields a non null item"));
                } else {
                    item.Enumerator.Current.Initialize(indexCaptured, this.Bind<ConduitT>());
                    if (item.Enumerator.Current.Id != indexCaptured) {
                        MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, new ChannelInitializationException($"Channel must initialize '{typeof(int).Name} {nameof(IConduit<ConduitT>.Id)}' by assigning the id parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked"));
                    }
                    if (item.Enumerator.Current.ChannelItem == null) {
                        MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, new ChannelInitializationException($"Channel must initialize '{nameof(Pipeline.IChannelState)} {nameof(IConduit<ConduitT>.ChannelItem)}' by assigning the conduitOwner parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked"));
                    }
                }
            } catch (Exception ex) {
                var value = item;
                MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, new ChannelInitializationException("Channel must receive a conduit that yields a non null value", ex));
            }
            stopwatch.Stop();
            typedState.verticalTicks += stopwatch.ElapsedTicks;
            typedState.isChanneling = true;
        }

        public void Clear() {
            _conduitMap.Clear();
            //throw new NotImplementedException();
        }

        public void Flush<ConduitT>(out IChannelState outState, out IEnumerable<ConduitT> passed, out IEnumerable<ErrorConduit> failed) where ConduitT : IConduit<ConduitT>{
            outState = null;
            List<ConduitT> passedList = new List<ConduitT>();
            passed = passedList;
            var failedList = new List<ErrorConduit>();
            failed = failedList;

            var conduitType = typeof(ConduitT);
            Stopwatch stopwatch = new Stopwatch();

            if (_conduitMap.TryGetValue(conduitType, out var state)) {
                ChannelState<ConduitT> typedState = state as ChannelState<ConduitT>;
                typedState.isChanneling = true;
                int chunkIndex = -1;
                while (chunkIndex * MaxChunkSize < typedState.itemValidMap.Count) {
                    chunkIndex++;

                    stopwatch.Restart();
                    //// Initialization
                    //for (int i = chunkIndex * MaxChunkSize; i < typedState.itemList.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                    //    //Filter based on parent ids.
                    //    if(!(typedState as IChannelState).ValidIds.Contains(i)) {
                    //        continue;
                    //    }
                    //    var item = typedState.itemList[i];
                    //    try {
                    //        if (item.Exception != null)
                    //            continue;
                    //        if (!item.Enumerator.MoveNext() && item.Enumerator.Current == null) {
                    //            item.Exception = new Exception("Channel must receive a conduit that yields a non null item");
                    //            typedState.itemList[i] = item;
                    //        } else {
                    //            item.Enumerator.Current.Initialize(i, this);
                    //            if (item.Enumerator.Current.Id != i) {
                    //                item.Exception = new Exception($"Channel must initialize '{typeof(int).Name} {nameof(IConduit<ConduitT>.Id)}' by assigning the id parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked");
                    //            }
                    //            if (item.Enumerator.Current.ChannelItem == null) {
                    //                item.Exception = new Exception($"Channel must initialize '{nameof(Pipeline.IChannelState)} {nameof(IConduit<ConduitT>.ChannelItem)}' by assigning the conduitOwner parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked");
                    //            }
                    //        }
                    //    } catch (Exception ex) {
                    //        var value = item;
                    //        value.Exception = new Exception("Channel must receive a conduit that yields a non null value", ex);
                    //        typedState.itemList[i] = value;
                    //    }
                    //}
                    //stopwatch.Stop();
                    //typedState.verticalTicks += stopwatch.ElapsedTicks;
                    typedState.isChanneling = true;
                    do {
                        stopwatch.Restart();
                        Debug.WriteLine($"Iteration: {_pipelineId}");
                        // Iteration;
                        for (int i = chunkIndex * MaxChunkSize; i < typedState.CurrentIndex && i < (chunkIndex + 1) * MaxChunkSize; i++) {//Filter based on parent ids.
                            if (!typedState.itemValidMap.TryGetValue(i, out var channel)) {
                                continue;
                            }
                            var conduit = channel.Enumerator.Current;
                            var item = typedState.itemValidMap[conduit.Id];

                            if (channel.Exception != null)
                                continue;
                            try {
                                var end = channel.Enumerator.MoveNext();
                                typedState.isChanneling &= !end;
                            } catch (Exception ex) {
                                MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, conduit.Id, new ChannelIterationException<ConduitT>("Error occurred while iterating", ex, typedState, conduit));
                            }
                        }
                        stopwatch.Stop();
                        typedState.verticalTicks += stopwatch.ElapsedTicks;
                        stopwatch.Restart();
                        typedState.Execute();
                        stopwatch.Stop();
                        typedState.horizontalTicks += stopwatch.ElapsedTicks;
                        typedState.isChanneling = !typedState.IsChanneling;
                    } while (state.IsChanneling);
                    // all iterators completed, run the operations for all conduits, but only once.
                    stopwatch.Restart();
                    Debug.WriteLine($"run the operations for all conduits: {_pipelineId}");
                    for (int i = chunkIndex * MaxChunkSize; i < typedState.CurrentIndex && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        if (!typedState.itemValidMap.TryGetValue(i, out var channel)) {
                            continue;
                        }
                        try {
                            channel.Operation(channel.Enumerator.Current);
                            if (channel.Exception == null && channel.Enumerator != null && channel.Enumerator.Current != null && channel.Exception == null)
                                passedList.Add((ConduitT)channel.Enumerator.Current);
                            else
                                failedList.Add(new ErrorConduit(i, channel.Exception ?? new Exception("Critical error [ba02c0dce4c040fbbfaae0478237896c]")));

                        } catch (Exception ex) {
                            MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, i, new ChannelOperationException<ConduitT>("Error on the channel's operation", ex, typedState, channel.Enumerator.Current));
                        }
                    }
                    stopwatch.Stop();
                    typedState.verticalTicks += stopwatch.ElapsedTicks;
                    
                }
                typedState.Clear();
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
