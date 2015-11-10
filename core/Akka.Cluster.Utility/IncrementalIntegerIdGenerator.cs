using System;

namespace Akka.Cluster.Utility
{
    public class IncrementalIntegerIdGenerator : IIdGenerator
    {
        private long _lastId;

        public Type IdType => typeof(long);

        public object GenerateId()
        {
            _lastId += 1;
            return _lastId;
        }
    }
}
