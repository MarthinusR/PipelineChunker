using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using static Mark2.Pipeline;

namespace Mark2 {
    public partial class Pipeline {
        private abstract class ChannelAbstract {
            protected ChannelAbstract(Pipeline pipeline) { Pipeline = pipeline; }
            public Pipeline Pipeline { get; private set; }
            public abstract void Flush();
        }
        private class ChannelClass<ConduitT> : ChannelAbstract, IChanel<ConduitT> where ConduitT : IConduit<ConduitT>, new() {
            /// <remarks>Anonymous (or static) methods that do not capture values</remarks>
            private static readonly Dictionary<MethodInfo, bool> _verifiedNonCapturingMethods = new Dictionary<MethodInfo, bool>();
            private readonly Pipeline _pipeline;
            private int total = 0;
            /// <remarks>Captures <c>public ConduitT Chunk</c>'s <c>ChunkInitializer</c> and <c>ChunkTransform</c> values in the key value pair ands maps it to wrappers</remarks>
            private readonly Dictionary<KeyValuePair<MethodInfo, MethodInfo>, IChunk> _chunkMethodsToChunkMap = new Dictionary<KeyValuePair<MethodInfo, MethodInfo>, IChunk>();
            private static readonly Type conduitType = typeof(ConduitT);
            private int channelingId = -1;
            ConduitWrapper[] wrapperArray;

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
            /// </summary>
            /// <typeparam name="StaticT"></typeparam>
            /// <typeparam name="InT"></typeparam>
            /// <typeparam name="OutT"></typeparam>
            /// <param name="ChunkInitializer"></param>
            /// <param name="ConduitInitializer"></param>
            /// <param name="ChunkTransform"></param>
            /// <param name="ConduitOperation"></param>
            /// <param name="Name"></param>
            /// <returns></returns>
            /// <exception cref="MethodIsCapturingException{ConduitT}"></exception>
            public ConduitT Chunk<StaticT, InT, OutT>(
                Func<IChanel<ConduitT>, StaticT> ChunkInitializer,
                Func<StaticT, InT> ConduitInitializer,
                Func<IChanel<ConduitT>, StaticT, Pair<ConduitT, InT>[], Pair<ConduitT, OutT>[]> ChunkTransform,
                Action<StaticT, Pair<ConduitT, OutT>> ConduitOperation,
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
                    _chunkMethodsToChunkMap[chunkKey] = chunk = new ChunkStruct<StaticT, InT, OutT>(_pipeline, this, ChunkInitializer, ChunkTransform);
                }
                //Debug.WriteLine($"Chunk - channelingId: {channelingId} [{this.GetHashCode()}]");
                if (wrapperArray[channelingId].currentChunk != null) {
                    throw new InvalidChunkInvocation<ConduitT>($"{(string.IsNullOrEmpty(Name) ? "" : $" with name '{Name}'")}. Only one invocation of Chunk occur per yield block");
                }
                if(ChunkTransform == null && !chunk.CanChunkTransformBeNull) {
                    throw new InvalidChunkInvocation<ConduitT>($"{(string.IsNullOrEmpty(Name) ? "" : $" with name '{Name}'")}. ChunkTransform cannot be null if InT and OutT generic parameters are not of the same type");
                }
                chunk.AddSpaceForOne();
                wrapperArray[channelingId].conduitInitializer = ConduitInitializer;
                wrapperArray[channelingId].conduitOperation = ConduitOperation;
                wrapperArray[channelingId].currentChunk = chunk;
                return wrapperArray[channelingId].enumerator.Current;                
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
            public void AddConduit(Action<ConduitT> channelInitializer, Action<ConduitT> channelFinalizer) {
                if (total >= Pipeline._maxChunkSize)
                    Flush();
                if(wrapperArray == null)
                    wrapperArray = new ConduitWrapper[Pipeline._maxChunkSize];
                wrapperArray[total] = new ConduitWrapper();

                ConduitT conduit = new ConduitT();
                conduit.Initialize(total, this, out var setException);
                if(conduit.Id != total) throw new ConduitInitializationException($"{conduitType.FullName} must assign the id provided in {nameof(IConduit<ConduitT>.Initialize)}");
                if(conduit.Channel != this) throw new ConduitInitializationException($"{conduitType.FullName} must assign the id channel in {nameof(IConduit<ConduitT>.Initialize)}");


                channelInitializer?.Invoke(conduit);
                wrapperArray[total].enumerator = conduit.GetEnumerator();
                wrapperArray[total].enumerator.MoveNext();

                if (((object)wrapperArray[total].enumerator.Current) != ((object)conduit)) {
                    throw new ConduitIterationException($"{conduitType.FullName} must yield 'this' on the first iteration");
                }
                wrapperArray[total].channelFinalizer = channelFinalizer;
                wrapperArray[total].setException = setException;
                total++;
            }

            public override void Flush() {
                bool isComplete = true;
                do {
                    isComplete = true;
                    // for all step once
                    for (channelingId = 0; channelingId < total; channelingId++) {
                        //Debug.WriteLine($"Flush - channelingId: {channelingId} [{this.GetHashCode()}]");
                        isComplete &= !wrapperArray[channelingId].enumerator.MoveNext();
                        //Debug.WriteLine($"Flush-MoveNext[done] - channelingId: {channelingId} [{this.GetHashCode()}]");
                    }
                    if (isComplete)
                        break;
                    // flush all the conduits by their chunk units (Note: chunk opts in for setting the wrappers current chunk to null.
                    foreach(var chunk in _chunkMethodsToChunkMap.Values) {
                        chunk.Flush(this, wrapperArray);
                    }

                }while(true);
                for (channelingId = 0; channelingId < total; channelingId++) {
                    if(wrapperArray[channelingId].channelFinalizer != null)
                        wrapperArray[channelingId].channelFinalizer(wrapperArray[channelingId].enumerator.Current);
                }
                Reset();
            }

            private void Reset() {
                total = 0;
            }
            private struct ConduitWrapper {
                public IEnumerator<ConduitT> enumerator;
                public Action<ConduitT> channelFinalizer;
                public Action<Exception> setException;
                public IChunk currentChunk;
                public Delegate conduitInitializer;
                public Delegate conduitOperation;
            }

            private interface IChunk {
                void Flush(ChannelClass<ConduitT> channel, ConduitWrapper[] wrapperArray);
                void AddSpaceForOne();
                bool CanChunkTransformBeNull {  get; }
            }
            /// <summary>
            /// Represents blocks of executable units
            /// </summary>
            /// <typeparam name="StaticT"></typeparam>
            /// <typeparam name="InT"></typeparam>
            /// <typeparam name="OutT"></typeparam>
            private class ChunkStruct<StaticT, InT, OutT> : IChunk {
                public int channelingId;
                public int total;
                Pipeline pipeline;
                ChannelClass<ConduitT> channel;
                StaticT data;
                int allocationSize;
                public bool CanChunkTransformBeNull => typeof(InT) == typeof(OutT);
                public void AddSpaceForOne() => allocationSize++;
                public Func<IChanel<ConduitT>, StaticT> chunkInitializer;
                public Func<IChanel<ConduitT>, StaticT, Pair<ConduitT, InT>[], Pair<ConduitT, OutT>[]> chunkTransform;

                public ChunkStruct(
                    Pipeline pipeline,
                    ChannelClass<ConduitT> channel,
                    Func<IChanel<ConduitT>, StaticT> ChunkInitializer,
                    Func<IChanel<ConduitT>, StaticT, Pair<ConduitT, InT>[], Pair<ConduitT, OutT>[]> ChunkTransform
                ) {
                    this.pipeline = pipeline;
                    this.channel = channel;
                    this.channelingId = -1;
                    this.total = 0;
                    this.chunkInitializer = ChunkInitializer;
                    this.chunkTransform = ChunkTransform;
                    this.data = ChunkInitializer(channel);
                }
                public void Flush(ChannelClass<ConduitT> channel, ConduitWrapper[] wrapperArray) {
                    Pair<ConduitT, InT>[] inputArray = new Pair<ConduitT, InT>[allocationSize];
                    var validInputWrapperIndices = new int[wrapperArray.Length];
                    int totalInputs = 0;
                    try {
                        // Load all the inputs
                        for (int i = 0; i < wrapperArray.Length; i++) {
                            var wrapper = wrapperArray[i];
                            // where this chunk is relevant
                            if (wrapper.currentChunk != this)
                                continue;
                            if (wrapper.conduitInitializer == null) {
                                // will be using the default value of the InT type
                                totalInputs++;
                            }
                            var current = wrapper.enumerator.Current;
                            try {
                                inputArray[totalInputs] = new Pair<ConduitT, InT>(current, ((Func<StaticT, InT>)wrapper.conduitInitializer)(data));
                                validInputWrapperIndices[totalInputs] = i;
                                totalInputs++;
                            } catch (Exception ex) {
                                wrapperArray[i].setException(ex);
                                if (wrapperArray[i].enumerator.Current.Exception != ex) {
                                    throw new ConduitInitializationException($"{conduitType.FullName} must assign the exception in the SetException action passed in {nameof(IConduit<ConduitT>.Initialize)}");
                                }
                            }
                        }
                        Pair<ConduitT, OutT>[] outputCollection;
                        // Transform the inputs
                        if (chunkTransform != null) {
                            // This is "static" so do not catch, should bubble up to the caller
                            outputCollection = chunkTransform(channel, data, inputArray);
                            if(outputCollection == null) {
                                throw new ChunkOperationException<ConduitT>(
                                    $" when invoking {nameof(IChanel<ConduitT>.Chunk)}'s ChunkTransform parameter. The returned value cannot be null.",
                                    new NullReferenceException());
                            }
                            if(outputCollection.Length != inputArray.Length) {
                                throw new ChunkOperationException<ConduitT>(
                                    $" when invoking {nameof(IChanel<ConduitT>.Chunk)}'s ChunkTransform parameter. The returned length of the collection must match the input length.",
                                    new IndexOutOfRangeException());
                            }
                        } else {
                            // NOTE: ChannelClass opts in cooperation that CanChunkTransformBeNull is satisfied.
                            outputCollection = (Pair<ConduitT, OutT>[])(object)inputArray;
                        }
                        // Provide the operation with the transformed inputs
                        for (int inputOutputIndex = 0; inputOutputIndex < totalInputs; inputOutputIndex++) {
                            int index = validInputWrapperIndices[inputOutputIndex];
                            var wrapper = wrapperArray[index];
                            if (wrapper.conduitOperation == null)
                                continue;
                            try {
                                ((Action<StaticT, Pair<ConduitT, OutT>>)wrapperArray[index].conduitOperation)(data, outputCollection[inputOutputIndex]);
                            } catch (Exception ex) {
                                wrapperArray[index].setException(ex);
                                if (wrapperArray[index].enumerator.Current.Exception != ex) {
                                    throw new ConduitInitializationException($"{conduitType.FullName} must assign the exception in the SetException action passed in {nameof(IConduit<ConduitT>.Initialize)}");
                                }
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
