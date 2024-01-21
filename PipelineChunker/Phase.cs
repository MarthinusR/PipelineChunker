using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PipelineChunker {
    public abstract class Phase : IPhase {
        public abstract IEnumerable<KeyValuePair<string, DataTable>> parameterTables { get; }
        public Action<DataTable, bool> Operation { get; private set; }

        public abstract DataSet Execute(IEnumerable<KeyValuePair<string, DataTable>> parameterTables);
        public void Init(Action<DataTable, bool> operation) {
            Operation = operation;
        }
    }

}
