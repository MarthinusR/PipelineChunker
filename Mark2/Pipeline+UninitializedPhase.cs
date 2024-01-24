using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public class UninitializedPhase : IPhase<int> {
            public Pipeline Pipeline => throw new NotImplementedException();
        }

    }
}
