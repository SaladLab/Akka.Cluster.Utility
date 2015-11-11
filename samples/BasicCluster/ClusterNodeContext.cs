using Akka.Actor;

namespace BasicCluster
{
    internal class ClusterNodeContext
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
    }
}
