using System;
using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Utility;

namespace Basic
{
    internal class EchoConsumerActor : ReceiveActor
    {
        private readonly ClusterNodeContext _clusterContext;
        private readonly UniqueAddress _clusterUniqueAddress;

        public EchoConsumerActor(ClusterNodeContext context)
        {
            _clusterContext = context;
            _clusterUniqueAddress = Cluster.Get(Context.System).SelfUniqueAddress;

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessages.RegisterActor(Self, typeof(EchoConsumerActor).Name));

            Receive<string>(m => OnMessage(m));
        }

        private void OnMessage(string s)
        {
            Console.WriteLine($"EchoConsumerActor({_clusterUniqueAddress.Address.Port}): {s}");
        }
    }
}
