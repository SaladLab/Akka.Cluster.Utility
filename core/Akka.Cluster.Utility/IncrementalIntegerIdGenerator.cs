using System;

namespace Akka.Cluster.Utility
{
    public class IncrementalIntegerIdGenerator : IIdGenerator<long>
    {
        private long _lastId;

        public void Initialize(object[] args)
        {
            if (args != null && args.Length == 1)
                _lastId = (long)args[0];
        }

        public long GenerateId()
        {
            _lastId += 1;
            return _lastId;
        }
    }
}
