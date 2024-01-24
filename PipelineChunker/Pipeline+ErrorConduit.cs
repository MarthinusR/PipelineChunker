using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        public class ErrorConduit : IConduit<ErrorConduit> {
            public Exception Exception { get; private set; }
            public int Id { get; private set; }

            public IChannelState ChannelItem => throw new NotImplementedException();

            public ErrorConduit(int id, Exception ex) {
                Exception = ex;
                Id = id;
            }

            public void Initialize(int id, IChannelState ChannelItem) {
                throw new NotImplementedException();
            }

            public IEnumerator<ErrorConduit> GetEnumerator() {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                throw new NotImplementedException();
            }
        }
    }
}
