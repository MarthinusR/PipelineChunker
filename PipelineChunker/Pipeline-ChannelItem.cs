using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
        private struct ChannelItem {
            public Action<IConduit> Operation;
            public IEnumerator<IConduit> Enumerator;
            public Exception Exception;
        }
    }
}
