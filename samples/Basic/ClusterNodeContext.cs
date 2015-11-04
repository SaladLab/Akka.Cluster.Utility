using Akka.Actor;

namespace Basic
{
    internal class ClusterNodeContext
    {
        public ActorSystem System;
        public IActorRef ClusterActorDiscovery;
    }
}
