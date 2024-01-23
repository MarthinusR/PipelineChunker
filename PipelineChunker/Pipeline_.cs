using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline {
        static int PipelineIdCounter = 0;
        //private Dictionary<Type, ChannelState<IEnumerator>> _conduitMap = new Dictionary<Type, ChannelState<IEnumerator>>();
        private Dictionary<Type, ChannelState> _conduitMap = new Dictionary<Type, ChannelState>();
        ChannelState<IEnumerator> _errorState;
        int _maxChunkSize;
        Pipeline _parent = null;
        readonly int _pipelineId = PipelineIdCounter++;
    }
}
