using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        private abstract class ChannelItem {
            public ChannelItem(int id) {
                this.Id = id;
            }
            public Exception Exception;
            public int Id;
        }
        private class ChannelItem<T> : ChannelItem {
            public ChannelItem(int id) : base(id){}
            public Action<IConduit<T>> Operation;
            public IEnumerator<IConduit<T>> Enumerator;
        }
    }
}
