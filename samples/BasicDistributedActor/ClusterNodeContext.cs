using Akka.Actor;

namespace BasicDistributedActor
{
    internal class ClusterNodeContext
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
    }
}
