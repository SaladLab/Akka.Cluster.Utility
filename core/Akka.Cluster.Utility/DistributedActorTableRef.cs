using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    // Intefaced actor-ref for DistributedActorTable<TKey>
    public sealed class DistributedActorTableRef<TKey>
    {
        public IActorRef Target { get; private set; }
        public TimeSpan? Timeout { get; private set; }

        public DistributedActorTableRef(IActorRef target)
            : this(target, null)
        {
        }

        public DistributedActorTableRef(IActorRef target, TimeSpan? timeout)
        {
            Target = target;
            Timeout = timeout;
        }

        public DistributedActorTableRef<TKey> WithTimeout(TimeSpan? timeout)
        {
            return new DistributedActorTableRef<TKey>(Target, timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.CreateReply> Create(object[] args)
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.CreateReply>(
                new DistributedActorTableMessage<TKey>.Create(args), Timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.CreateReply> Create(TKey id, object[] args)
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.CreateReply>(
                new DistributedActorTableMessage<TKey>.Create(id, args), Timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.GetOrCreateReply> GetOrCreate(TKey id, object[] args)
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.GetOrCreateReply>(
                new DistributedActorTableMessage<TKey>.GetOrCreate(id, args), Timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.GetReply> Get(TKey id)
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.GetReply>(
                new DistributedActorTableMessage<TKey>.Get(id), Timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.GetIdsReply> GetIds()
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.GetIdsReply>(
                new DistributedActorTableMessage<TKey>.GetIds(), Timeout);
        }

        public void GracefulStop(object stopMessage)
        {
            Target.Tell(new DistributedActorTableMessage<TKey>.GracefulStop(stopMessage));
        }
    }

    // Intefaced actor-ref for DistributedActorTableContainer<TKey>
    public class DistributedActorTableContainerRef<TKey>
    {
        public IActorRef Target { get; private set; }
        public TimeSpan? Timeout { get; private set; }

        public DistributedActorTableContainerRef(IActorRef target)
            : this(target, null)
        {
        }

        public DistributedActorTableContainerRef(IActorRef target, TimeSpan? timeout)
        {
            Target = target;
            Timeout = timeout;
        }

        public DistributedActorTableContainerRef<TKey> WithTimeout(TimeSpan? timeout)
        {
            return new DistributedActorTableContainerRef<TKey>(Target, timeout);
        }

        public Task<DistributedActorTableMessage<TKey>.AddReply> Add(TKey id, IActorRef actor)
        {
            return Target.Ask<DistributedActorTableMessage<TKey>.AddReply>(
                new DistributedActorTableMessage<TKey>.Add(id, actor), Timeout);
        }

        public void Remove(TKey id)
        {
            Target.Tell(new DistributedActorTableMessage<TKey>.Remove(id));
        }
    }
}
