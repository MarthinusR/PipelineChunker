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
                return !_channelMap.Values.All(x => !x.IsOpen);
            }
        }

        public int MaxChunkSize => _maxChunkSize;

        public IEnumerable<int> ValidIds => _channelMap.SelectMany(x => x.Value.ValidIds);

        public Pipeline.IChannelState Bind<ConduitT>() {
            var conduitType = typeof(ConduitT);
            if (!_channelMap.TryGetValue(conduitType, out var state)) {
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
            if (!_channelMap.TryGetValue(conduitType, out var state)) {
                Debug.WriteLine($"New {this._pipelineId}");
                _channelMap[conduitType] = state = typedState = new ChannelState<ConduitT>(this);
            }
            typedState = (ChannelState<ConduitT>)state;
            Debug.WriteLine($"Chanell<{typeof(ConduitT).FullName}> pipeId: {_pipelineId}");
            int indexCaptured = typedState.GetNextAndStepValidId();
            Debug.WriteLine($"Chanell<{typeof(ConduitT).FullName}> pipeId: {_pipelineId}, captured id: {indexCaptured}");
            ChannelItem<ConduitT> item = new ChannelItem<ConduitT>(indexCaptured) {
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
                    var boundState = this.Bind<ConduitT>();
                    item.Enumerator.Current.Initialize(indexCaptured, boundState);
                    if (item.Enumerator.Current.Id != indexCaptured) {
                        MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, indexCaptured, new ChannelInitializationException($"Channel must initialize '{typeof(int).Name} {nameof(IConduit<ConduitT>.Id)}' by assigning the id parameter when {conduitType.FullName}.{nameof(IConduit<ConduitT>.Initialize)} is invoked"));
                    }
                    if (item.Enumerator.Current.ChannelItem != boundState) {
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
            _channelMap.Clear();
            //throw new NotImplementedException();
        }

        public void Flush<ConduitT>(out IChannelState outState, out Dictionary<int, ConduitT> passed, out Dictionary<int, ErrorConduit> failed) where ConduitT : IConduit<ConduitT>{
            outState = null;
            passed = new Dictionary<int, ConduitT>();
            failed = new Dictionary<int, ErrorConduit>();

            var conduitType = typeof(ConduitT);
            Stopwatch stopwatch = new Stopwatch();

            if (_channelMap.TryGetValue(conduitType, out var state)) {
                ChannelState<ConduitT> typedState = state as ChannelState<ConduitT>;
                typedState.isChanneling = true;
                int chunkIndex = -1;
                while (chunkIndex * MaxChunkSize < typedState.itemValidMap.Count) {
                    chunkIndex++;

                    stopwatch.Restart();
                    typedState.isChanneling = true;
                    do {
                        stopwatch.Restart();
                        Debug.WriteLine($"Iteration: {_pipelineId}");
                        foreach(int vid in ValidIds) {
                            Debug.WriteLine($"!!!!!!!!!!!  {vid}");
                        }


                        // Iteration;
                        for (int i = chunkIndex * MaxChunkSize; i < typedState.itemValidMap.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {//Filter based on parent ids.
                            
                            var channelAbstract = typedState.itemValidMap.ElementAt(i).Value;

                            ChannelItem<ConduitT> channel = channelAbstract as ChannelItem<ConduitT>;
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
                    for (int i = chunkIndex * MaxChunkSize; i < typedState.itemValidMap.Count && i < (chunkIndex + 1) * MaxChunkSize; i++) {
                        var channelAbstract = typedState.itemValidMap.ElementAt(i).Value;
                        ChannelItem<ConduitT> channel = null;
                        try {
                            channel = channelAbstract as ChannelItem<ConduitT>;
                            channel.Operation(channel.Enumerator.Current);
                            if (channel.Exception == null && channel.Enumerator != null && channel.Enumerator.Current != null && channel.Exception == null)
                                passed[i] = (ConduitT)channel.Enumerator.Current;
                            else
                                failed[i] = new ErrorConduit(i, null);

                        } catch (Exception ex) {
                            MoveToErrorMap<ChannelState<ConduitT>, ConduitT>(typedState, i, new ChannelOperationException<ConduitT>("Error on the channel's operation", ex, typedState, channel?.Enumerator?.Current));
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
            if (_channelMap.TryGetValue(typeof(ConduitT), out var item)) {
                IsOpen = item.IsOpen;
                IsChanneling = item.IsChanneling;
            } else {
                IsOpen = false;
                IsChanneling = false;
            }
        }

    }
}
