using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public interface IChanel {
            Pipeline Pipeline { get; }
            string Name { get; }
        }

        public interface IChannel<ConduitT> : IChanel where ConduitT: Conduit<ConduitT>, new(){
            
        }
    }
}
