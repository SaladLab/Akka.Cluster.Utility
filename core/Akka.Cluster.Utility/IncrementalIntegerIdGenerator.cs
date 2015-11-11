using System;

namespace Akka.Cluster.Utility
{
    public class IncrementalIntegerIdGenerator : IIdGenerator
    {
        private long _lastId;

        public Type IdType => typeof(long);

        public void Initialize(object[] args)
        {
            if (args != null && args.Length == 1)
                _lastId = (long)args[0];
        }

        public object GenerateId()
        {
            _lastId += 1;
            return _lastId;
        }
    }
}
