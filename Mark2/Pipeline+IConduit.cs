using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public class Crazy {}
        public interface IConduit<T> : IEnumerable<T> where T : IConduit<T> {
            IChanel<T> Channel { get; }
            Exception Exception { get; }

            void Initialize(IChanel<T> Channel, out Action<Exception> SetException);
        }
    }
}
