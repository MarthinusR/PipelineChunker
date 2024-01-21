using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        private class ChannelState : IChannelState {
            public bool IsChanneling;
            public bool IsOpen;
            public List<ChannelItem> list;

            public List<IPhase> phaseList = null;

            public long verticalTicks = 0;
            public long horizontalTicks = 0;

            //TODO: should this not just be a list?
            public Dictionary<Type, List<IPhase>> phaseMap = new Dictionary<Type, List<IPhase>>();

            public IEnumerable<KeyValuePair<String, DataTable>> parameterTables;

            double IChannelState.VerticalSeconds => verticalTicks / (double)Stopwatch.Frequency;

            double IChannelState.HorizontalSeconds => horizontalTicks / (double)Stopwatch.Frequency;

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
                var set = phaseList.First().Execute(parameterTables);
                foreach (var phaseList in phaseMap.Values) {
                    for (int i = 0; i < phaseList.Count; i++) {
                        bool isError = false;
                        phaseList[i].Operation(set.Tables[i], isError);
                    }
                }
                phaseList = null;
                parameterTables = null;
                phaseMap.Clear();
            }

            public PhaseT Chunk<PhaseT>(IConduit conduit, Action<DataRow> rowLoader, Action<DataTable, bool> operation) where PhaseT : IPhase, new() {
                IPhase phase;
                phaseList = phaseList ?? new List<IPhase>();
                phaseList.Add(phase = new PhaseT());
                phase.Init(operation);
                parameterTables = parameterTables ?? phase.parameterTables;
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
