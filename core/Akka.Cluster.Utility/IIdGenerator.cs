using System;

namespace Akka.Cluster.Utility
{
    public interface IIdGenerator
    {
        Type IdType { get; }
        object GenerateId();
    }
}
