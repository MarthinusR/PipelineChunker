using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        public class ChannelInitializationException : Exception {
            public ChannelInitializationException(string message) : base(message) { }
            public ChannelInitializationException(string message, Exception ex) : base(message, ex) { }
        }
        public class ChannelIterationException<T> : Exception {
            public ChannelIterationException(string message, Exception ex, IChannelState state, IConduit<T> conduit) : base(message, ex) { }
        }
        public class ChannelOperationException<T> : Exception {
            public ChannelOperationException(string message, Exception ex, IChannelState state, IConduit<T> conduit) : base(message, ex) { }
        }
    }
}
