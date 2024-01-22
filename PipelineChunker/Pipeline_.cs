using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
        //private Dictionary<Type, ChannelState<IEnumerator>> _conduitMap = new Dictionary<Type, ChannelState<IEnumerator>>();
        private Dictionary<Type, IChannelState> _conduitMap = new Dictionary<Type, IChannelState>();
        ChannelState<IEnumerator> _errorState;
        int _maxChunkSize;
    }
}
