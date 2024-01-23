using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline  {
        public interface IChannelState {
            PhaseT Chunk<IConduitT, PhaseT>(IConduitT conduit, Action<DataRow> value, Action<DataTable, bool> value1) where PhaseT : IPhase, new();
            double VerticalSeconds { get; }
            double HorizontalSeconds { get; }
            Pipeline Pipeline { get; }
            bool IsChanneling { get; }
            bool IsOpen { get; }
            IEnumerable<int> ValidIds { get; }
        }
    }
}
