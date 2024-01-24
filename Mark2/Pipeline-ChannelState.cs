using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        private class ChannelState {
            //public readonly List<Phase> phaseList = new List<Phase>();
            private readonly Pipeline _pipeline;

            public ChannelState(Pipeline pipeline) {
                _pipeline = pipeline;
            }
        }
    }
}
