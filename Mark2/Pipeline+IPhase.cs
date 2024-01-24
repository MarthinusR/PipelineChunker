using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public interface IPhase<DataT> {
            Pipeline Pipeline { get; }

        }
    }
}
