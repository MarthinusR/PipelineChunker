using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using static PipelineChunker.Pipeline;

namespace PipelineChunker {
    public partial class Pipeline {
        private abstract class ChannelAbstract {
            protected ChannelAbstract(Pipeline pipeline) { Pipeline = pipeline; }
            public Pipeline Pipeline { get; private set; }
            public abstract void Flush();
        }
        private class ChannelClass<ConduitT> : ChannelAbstract, IChannel<ConduitT> where ConduitT : Conduit<ConduitT>, new() {
            /// <remarks>Anonymous (or static) methods that do not capture values</remarks>
            private static readonly Dictionary<MethodInfo, bool> _verifiedNonCapturingMethods = new Dictionary<MethodInfo, bool>();
            private readonly Pipeline _pipeline;
            private int total = 0;
            /// <remarks>Captures <c>public ConduitT Chunk</c>'s <c>ChunkInitializer</c> and <c>ChunkTransform</c> values in the key value pair ands maps it to wrappers</remarks>
            private readonly Dictionary<KeyValuePair<MethodInfo, MethodInfo>, IChunk> _chunkMethodsToChunkMap = new Dictionary<KeyValuePair<MethodInfo, MethodInfo>, IChunk>();
            private static readonly Type conduitType = typeof(ConduitT);
            /// <summary>
            /// Stores pristine and error wrappers to tht left and right respectfully.
            /// </summary>
            /// <remarks>
            /// [pristine1, pristine2, pristine3, empty, empty, error3, error2, error1]
            /// </remarks>
            ConduitWrapper[] wrapperPristineAndErrorArray;
            int errorCount = 0;
            ExceptionCommunicator communicator = new ExceptionCommunicator();

            private static int _IdCounter = 0;
            private readonly int id = _IdCounter++;
            public int Id => id;
            public string Name { get; private set; }

            public ChannelClass(Pipeline pipeline) : base(pipeline) {
                Debug.WriteLine($"New ChannelClass {Id}");
                _pipeline = pipeline;
            }
            /// <remarks>
            /// NOTE: at this point the conduit's enumerator should already have been initialized (at least a call to GetEnumerator (TODO: confirm))
            /// </remarks>
            /// <summary>
            /// 
            /// <exception cref="MethodIsCapturingException{ConduitT}"></exception>
            public void Chunk<StaticT, InT, OutT>(
                Conduit<ConduitT> conduit,
                ChunkType<ConduitT, StaticT, InT, OutT>.ChunkInitializer ChunkInitializer,
                ChunkType<ConduitT, StaticT, InT, OutT>.ConduitInitializer ConduitInitializer,
                ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform ChunkTransform,
                ChunkType<ConduitT, StaticT, InT, OutT>.ConduitOperation ConduitOperation,
                string Name = null
            ) {
                var chunkKey = new KeyValuePair<MethodInfo, MethodInfo>(ChunkInitializer.Method, ChunkTransform.Method);
                if (!_chunkMethodsToChunkMap.TryGetValue(chunkKey, out var chunk)) {
                    //Check if ChunkInitializer and ChunkTransform is static (or that they are not capturing variables)
                    //  because these methods will only be invoked on a per-chunk basis instead of for each conduit.
                    if (!IsMethodNoCapturing(ChunkInitializer.Method, out string synopsisA)) {
                        throw new MethodIsCapturingException<ConduitT>($"[{synopsisA}] in the anonymous method passed to the {nameof(ChunkInitializer)} parameter", ChunkInitializer.Method);
                    }
                    if (!IsMethodNoCapturing(ChunkTransform.Method, out string synopsisB)) {
                        throw new MethodIsCapturingException<ConduitT>($"[{synopsisB}] in the anonymous method passed to the {nameof(ChunkTransform)} parameter", ChunkTransform.Method);
                    }
                    _chunkMethodsToChunkMap[chunkKey] = chunk = new ChunkHandler<StaticT, InT, OutT>(_pipeline, this, ChunkInitializer, ChunkTransform);
                }
                int channelingId = conduit.ConduitId;
                if (wrapperPristineAndErrorArray[channelingId].currentChunk != null) {
                    throw new InvalidChunkInvocation<ConduitT>($"{(string.IsNullOrEmpty(Name) ? "" : $" with name '{Name}'")}. Only one invocation of Chunk occur per yield block");
                }
                if(ChunkTransform == null && !chunk.CanChunkTransformBeNull) {
                    throw new InvalidChunkInvocation<ConduitT>($"{(string.IsNullOrEmpty(Name) ? "" : $" with name '{Name}'")}. ChunkTransform cannot be null if InT and OutT generic parameters are not of the same type");
                }
                chunk.AddSpaceForOne();
                wrapperPristineAndErrorArray[channelingId].conduitInitializer = ConduitInitializer;
                wrapperPristineAndErrorArray[channelingId].conduitOperation = ConduitOperation;
                wrapperPristineAndErrorArray[channelingId].currentChunk = chunk;         
            }

            bool IsMethodNoCapturing(MethodInfo info, out string synopsis) {
                synopsis = null;
                if (!_verifiedNonCapturingMethods.TryGetValue(info, out bool value)) {
                    _verifiedNonCapturingMethods[info] = info.IsStatic
                                                               || info.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;
                    if (!_verifiedNonCapturingMethods[info]) {
                        synopsis = info.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Skip(1).Select(x => $"{x.FieldType.FullName} {x.Name};" ).Aggregate((a, b) => a + b);
                    }
                }
                return _verifiedNonCapturingMethods[info];
            }
            public void AddConduit(Action<ConduitT> ChannelInitializer, Action<ConduitT, Exception, ExceptionCommunicator> ChannelFinalizer) {
                if (total >= Pipeline._maxChunkSize)
                    Flush();

                // Inititialize a new conduit
                ConduitT conduit = new ConduitT();
                (conduit as IConduit<ConduitT>).Channel = this;
                (conduit as IConduit<ConduitT>).ChannelId = total;
                // NOTE: do not catch, let the caller handle this
                ChannelInitializer?.Invoke(conduit);

                // Lazy allocation
                if (wrapperPristineAndErrorArray == null)
                    wrapperPristineAndErrorArray = new ConduitWrapper[Pipeline._maxChunkSize];

                wrapperPristineAndErrorArray[total] = new ConduitWrapper();
                wrapperPristineAndErrorArray[total].enumerator = (IEnumerator<ConduitT>)conduit.GetEnumerator();
                wrapperPristineAndErrorArray[total].enumerator.MoveNext();

                if (((object)wrapperPristineAndErrorArray[total].enumerator.Current) != ((object)conduit)) {
                    throw new ConduitIterationException($"{conduitType.FullName} must yield 'this' on the first iteration");
                }
                wrapperPristineAndErrorArray[total].channelFinalizer = ChannelFinalizer;
                total++;
            }

            public override void Flush() {
                try {
                    bool isComplete = true;
                    do {
                        isComplete = true;
                        // flush all the conduits' chunk units (Note: chunk opts in for setting the wrappers current chunk to null.
                        foreach (var chunk in _chunkMethodsToChunkMap.Values) {
                            chunk.Flush(this, wrapperPristineAndErrorArray, communicator);
                        }
                        // for all step once
                        for (int channelingId = 0; channelingId < total; channelingId++) {
                            try {
                                isComplete &= !wrapperPristineAndErrorArray[channelingId].enumerator.MoveNext();
                            } catch (Exception ex) {
                                isComplete &= true;
                                wrapperPristineAndErrorArray[channelingId].Exception = ex;
                            }
                        }
                        if (isComplete)
                            break;

                    } while (true);

                    void MoveWrapperToErrorLocation(int id) {
                        total--;
                        errorCount++;
                        int errorIndex = wrapperPristineAndErrorArray.Length - errorCount;
                        if (total >= errorIndex) {
                            // we must swap, as there is overlap
                            ConduitWrapper temp = wrapperPristineAndErrorArray[errorIndex];
                            wrapperPristineAndErrorArray[errorIndex] = wrapperPristineAndErrorArray[id];
                            wrapperPristineAndErrorArray[id] = temp;
                        } else {
                            // no need for swap, as the space is unused.
                            wrapperPristineAndErrorArray[errorIndex] = wrapperPristineAndErrorArray[id];
                            // swap the pristine section's tail into this location
                            if (id < total) {
                                wrapperPristineAndErrorArray[id] = wrapperPristineAndErrorArray[total];
                            }
                        }
                    }

                    for (int channelingId = 0; channelingId < total; channelingId++) {
                        if (wrapperPristineAndErrorArray[channelingId].channelFinalizer != null) {
                            communicator.ExceptionHandled = false;
                            Exception exception = wrapperPristineAndErrorArray[channelingId].Exception;
                            wrapperPristineAndErrorArray[channelingId].channelFinalizer(wrapperPristineAndErrorArray[channelingId].enumerator.Current, exception, communicator);
                            if (exception != null && !communicator.ExceptionHandled) {
                                MoveWrapperToErrorLocation(channelingId);
                            }
                        } else if (wrapperPristineAndErrorArray[channelingId].Exception != null) {
                            MoveWrapperToErrorLocation(channelingId);
                        }
                    }
                    if (errorCount > 0) {
                        // now compound all the error wrappers and throw
                        CompoundException<ConduitT> compound = new CompoundException<ConduitT>();
                        for (int i = wrapperPristineAndErrorArray.Length - 1; i >= wrapperPristineAndErrorArray.Length - errorCount; i--) {
                            var wrapper = wrapperPristineAndErrorArray[i];
                            compound.Add(wrapper.enumerator.Current, wrapper.Exception);
                        }
                        throw compound;
                    }
                } finally {
                    Reset();
                }
            }

            private void Reset() {
                total = 0;
                errorCount = 0;
            }
            private class ConduitWrapper {
                public IEnumerator<ConduitT> enumerator;
                public Action<ConduitT, Exception, ExceptionCommunicator> channelFinalizer;
                public Exception Exception;
                public IChunk currentChunk;
                public Delegate conduitInitializer;
                public Delegate conduitOperation;
            }

            private interface IChunk {
                void Flush(ChannelClass<ConduitT> channel, ConduitWrapper[] wrapperArray, ExceptionCommunicator communicator);
                void AddSpaceForOne();
                bool CanChunkTransformBeNull {  get; }
            }
            /// <summary>
            /// Represents blocks of executable units
            /// </summary>
            /// <typeparam name="StaticT"></typeparam>
            /// <typeparam name="InT"></typeparam>
            /// <typeparam name="OutT"></typeparam>
            private class ChunkHandler<StaticT, InT, OutT> : IChunk {
                public int channelingId;
                public int total;
                Pipeline pipeline;
                ChannelClass<ConduitT> channel;
                StaticT data;
                int allocationSize;
                public bool CanChunkTransformBeNull => typeof(InT) == typeof(OutT);
                public void AddSpaceForOne() => allocationSize++;
                public ChunkType<ConduitT, StaticT, InT, OutT>.ChunkInitializer chunkInitializer;
                public ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform chunkTransform;

                public ChunkHandler(
                    Pipeline pipeline,
                    ChannelClass<ConduitT> channel,
                    ChunkType<ConduitT, StaticT, InT, OutT>.ChunkInitializer ChunkInitializer,
                    ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform ChunkTransform
                ) {
                    this.pipeline = pipeline;
                    this.channel = channel;
                    this.channelingId = -1;
                    this.total = 0;
                    this.chunkInitializer = ChunkInitializer;
                    this.chunkTransform = ChunkTransform;
                    this.data = ChunkInitializer(channel);
                }
                public void Flush(ChannelClass<ConduitT> channel, ConduitWrapper[] wrapperArray, ExceptionCommunicator communicator) {
                    if (allocationSize == 0)
                        return;
                    Item<ConduitT, InT>[] inputArray = new Item<ConduitT, InT>[allocationSize];
                    var validInputWrapperIndices = new int[wrapperArray.Length];
                    int totalPristine = 0;
                    int totalFail = 0;
                    try {
                        // Load all the inputs
                        for (int i = 0; totalPristine < allocationSize && i < wrapperArray.Length; i++) {
                            var wrapper = wrapperArray[i];
                            // where this chunk is relevant
                            if (wrapper.currentChunk != this)
                                continue;
                            if (wrapper.conduitInitializer == null) {
                                // will be using the default value of the InT type
                                totalPristine++;
                            }
                            var current = wrapper.enumerator.Current;
                            try {
                                inputArray[totalPristine] = new Item<ConduitT, InT>(current, wrapper.Exception, ((ChunkType<ConduitT, StaticT, InT, OutT>.ConduitInitializer)wrapper.conduitInitializer)(data));
                                validInputWrapperIndices[totalPristine] = i;
                                totalPristine++;
                            } catch (Exception ex) {
                                totalFail++;
                                wrapper.Exception = ex;
                                inputArray[inputArray.Length - totalFail] = new Item<ConduitT, InT>(current, wrapper.Exception, default);
                            }
                        }
                        Item<ConduitT, OutT>[] outputCollection;
                        // Transform the inputs
                        if (chunkTransform != null) {
                            // This is "static" so do not catch, should bubble up to the caller
                            //outputCollection = chunkTransform(channel, data, inputArray, totalInputs);
                            outputCollection = chunkTransform(channel, data, inputArray, totalPristine);
                            if(outputCollection == null) {
                                throw new ChunkOperationException<ConduitT>(
                                    $" when invoking {nameof(Conduit<ConduitT>)}.Chunk's {nameof(ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform)} parameter. The returned value cannot be null.",
                                    new NullReferenceException());
                            }
                            if(outputCollection.Length != inputArray.Length) {
                                throw new ChunkOperationException<ConduitT>(
                                    $" when invoking {nameof(Conduit<ConduitT>)}.Chunk's {nameof(ChunkType<ConduitT, StaticT, InT, OutT>.ChunkTransform)} parameter. The returned length of the collection must match the input length.",
                                    new IndexOutOfRangeException());
                            }
                        } else {
                            // NOTE: ChannelClass opts in cooperation that CanChunkTransformBeNull is satisfied.
                            outputCollection = (Item<ConduitT, OutT>[])(object)inputArray;
                        }
                        // Provide the operation with the transformed inputs
                        for (int inputOutputIndex = 0; inputOutputIndex < totalPristine; inputOutputIndex++) {
                            int index = validInputWrapperIndices[inputOutputIndex];
                            var wrapper = wrapperArray[index];
                            if (wrapper.conduitOperation == null)
                                continue;
                            try {
                                ((ChunkType<ConduitT, StaticT, InT, OutT>.ConduitOperation)wrapper.conduitOperation)(data, outputCollection[inputOutputIndex], wrapper.Exception, communicator);
                                if(wrapper.Exception != null && !communicator.ExceptionHandled) {
                                    throw wrapper.Exception;
                                }
                            } catch (Exception ex) {
                                wrapper.Exception = inputArray[inputOutputIndex].Exception = ex;
                            }
                        }
                    }catch {
                        throw;
                    } finally {
                        // Set the current chunk to null for all applicable conduits
                        for(int i = 0; i < wrapperArray.Length; i++) {
                            if (wrapperArray[i].currentChunk != this)
                                continue;
                            wrapperArray[i].currentChunk = null;
                        }
                        Reset();
                    }
                }
                void Reset() {
                    allocationSize = 0;
                }
            }
        }
    }
}
