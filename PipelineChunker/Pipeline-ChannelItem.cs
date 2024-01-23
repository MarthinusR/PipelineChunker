using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        private class ChannelItem<T> {
            public Action<IConduit<T>> Operation;
            public IEnumerator<IConduit<T>> Enumerator;
            public Exception Exception;
        }
    }
}
