using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PipelineChunker {
    public interface IPhase {
        void Init(Action<DataTable, bool> operation);
        IEnumerable<KeyValuePair<String, DataTable>> parameterTables { get; }
        DataSet Execute(IEnumerable<KeyValuePair<String, DataTable>> parameterTables);
        Action<DataTable, bool> Operation { get; }
    }
}
