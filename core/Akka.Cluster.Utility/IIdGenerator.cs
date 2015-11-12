using System;

namespace Akka.Cluster.Utility
{
    public interface IIdGenerator<out TKey>
    {
        void Initialize(object[] args);
        TKey GenerateId();
    }
}
