using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        public class ErrorConduit {
            public Exception Exception { get; private set; }
            public int Id { get; private set; }
            public ErrorConduit(int id, Exception ex) {
                Exception = ex;
                Id = id;
            }
        }
    }
}
