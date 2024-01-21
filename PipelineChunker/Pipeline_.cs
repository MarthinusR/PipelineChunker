using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace PipelineChunker {
    public partial class Pipeline : IPipeline {
        private Dictionary<Type, ChannelState> _conduitMap = new Dictionary<Type, ChannelState>();
        ChannelState _errorState;  
    }
}
