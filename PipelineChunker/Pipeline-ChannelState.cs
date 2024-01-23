using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        private abstract class ChannelState : IChannelState {
            public abstract double VerticalSeconds { get; }
            public abstract double HorizontalSeconds { get; }
            public abstract Pipeline Pipeline { get; }
            public abstract bool IsChanneling { get; }
            public abstract bool IsOpen { get; }
            public abstract IEnumerable<int> ValidIds { get; }

            public abstract PhaseT Chunk<IConduitT, PhaseT>(IConduitT conduit, Action<DataRow> value, Action<DataTable, bool> value1) where PhaseT : IPhase, new();
        }
        private class ChannelState<T> : ChannelState {
            public ChannelState(Pipeline pipeline) {
                Debug.WriteLine($"Channel got pipline {pipeline._pipelineId}");
                this.ownerPipeline = pipeline;
                itemValidMap = new Dictionary<int, ChannelItem<T>>();
                itemErrorMap = new Dictionary<int, ChannelItem<T>>();
                thisPipeline = new Pipeline(pipeline, pipeline.MaxChunkSize);
                startIndex = pipeline == null ? 0 : pipeline.ValidIds.DefaultIfEmpty().Min();
            }
            public bool isChanneling;
            public bool isOpen;
            public Dictionary<int, ChannelItem<T>> itemValidMap;
            public Dictionary<int, ChannelItem<T>> itemErrorMap;

            public readonly int startIndex;
            public int CurrentIndex { get; private set; }

            public List<IPhase> phaseList = null;

            public long verticalTicks = 0;
            public long horizontalTicks = 0;
            private readonly Pipeline ownerPipeline;
            private readonly Pipeline thisPipeline;

            //TODO: should this not just be a list?
            public Dictionary<Type, List<IPhase>> phaseMap = new Dictionary<Type, List<IPhase>>();

            public IEnumerable<KeyValuePair<String, DataTable>> parameterTables;

            public override double VerticalSeconds => verticalTicks / (double)Stopwatch.Frequency;

            public override double HorizontalSeconds => horizontalTicks / (double)Stopwatch.Frequency;

            public void Clear() {
                itemValidMap.Clear();
                itemErrorMap.Clear();
            }
            public int GetNextAndStepValidId() {
                if (ownerPipeline._parent != null) {
                    //all the paths of the parent should be initialized by now
                    //so select the next valid id (the min of all ids less than this id)
                    return ownerPipeline._parent.ValidIds.Where(x => x > CurrentIndex).DefaultIfEmpty().Min();
                }
                return CurrentIndex++;
            }
            public override IEnumerable<int> ValidIds { get {
                    var conduitType = typeof(T);

                    return itemValidMap.Values.Where(x => {
                        if (ownerPipeline != null) {
                            foreach(var otherPair in thisPipeline._conduitMap) {
                                if (otherPair.Value == this)
                                    continue;
                                if (otherPair.Value.ValidIds.All(xx => itemValidMap.Values.Contains(x)))
                                    return false;
                            }
                        }
                        return x.Enumerator != null && x.Enumerator.Current != null && x.Exception == null;
                    }).Select(x => x.Enumerator.Current.Id);
                } 
            }// => list.Where(x => x.Enumerator != null && x.Enumerator.Current != null && x.Exception == null).Select(x => x.Enumerator.Current.Id);

            public override Pipeline Pipeline => thisPipeline;

            public override bool IsChanneling => isChanneling;
            public override bool IsOpen => isOpen;

            public string GetParameterSignature() {
                StringBuilder sbForProcParametersHash = new StringBuilder();
                foreach (var pair in parameterTables.OrderBy(x => x.Key)) {
                    sbForProcParametersHash.Append(pair.Key);
                    foreach (var col in pair.Value.Columns.Cast<DataColumn>().OrderBy(x => x.ColumnName)) {
                        sbForProcParametersHash.Append(col.ColumnName);
                    }
                }
                return sbForProcParametersHash.ToString();
            }

            public void Execute() {
                if (phaseList == null)
                    return;
                int lastIndex = 0;
                try {
                    var set = phaseList.First().Collect(this, parameterTables);
                    foreach (var phaseList in phaseMap.Values) {
                        for (int i = 0; i < phaseList.Count; i++) {
                            lastIndex = i;
                            bool isError = false;
                            phaseList[i].Operation(set.Tables[i], isError);
                        }
                    }
                    phaseList = null;
                    parameterTables = null;
                }catch (Exception ex) {
                    Debug.WriteLine($"TODO {lastIndex} a24408c1a37c41bba1fafd1c5bf1ec61\n{ex}");
                }
                phaseMap.Clear();
            }

            public override PhaseT Chunk<IConduitT, PhaseT>(IConduitT conduit, Action<DataRow> rowLoader, Action<DataTable, bool> operation) {
                IPhase phase;
                phaseList = phaseList ?? new List<IPhase>();
                phaseList.Add(phase = new PhaseT());
                phase.Init(operation);
                parameterTables = parameterTables ?? phase.ParameterTables;
                if (!phaseMap.TryGetValue(phase.GetType(), out var list)) {
                    phaseMap[phase.GetType()] = list = new List<IPhase>();
                }
                list.Add(phase);
                foreach (var pair in parameterTables) {
                    var row = pair.Value.NewRow();
                    rowLoader(row);
                    pair.Value.Rows.Add(row);
                }
                return (PhaseT)phase;
            }
        }
    }
}
