using System;
using System.Collections.Generic;
using System.Text;

namespace Mark2 {
    public partial class Pipeline {
        public interface IConduit<T> : IEnumerable<T> where T : IConduit<T> {
            int Id { get; }
            IChanel<T> Channel { get; }
            Exception Exception { get; }

            void Initialize(int Id, IChanel<T> Channel, out Action<Exception> SetException);
        }
    }
}
