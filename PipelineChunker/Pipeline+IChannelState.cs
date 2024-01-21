using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
        public interface IChannelState {
            PhaseT Chunk<PhaseT>(IConduit conduit, Action<DataRow> value, Action<DataTable, bool> value1) where PhaseT : IPhase, new();
            double VerticalSeconds { get; }
            double HorizontalSeconds { get; }
        }
    }
}
