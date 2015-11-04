using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Cluster.Utility;

namespace Basic
{
    internal class EchoProducerActor : ReceiveActor
    {
        private readonly ClusterNodeContext _clusterContext;
        private readonly List<IActorRef> _consumers = new List<IActorRef>();
        private int _consumerCursor = 0;

        public EchoProducerActor(ClusterNodeContext context)
        {
            _clusterContext = context;

            _clusterContext.ClusterActorDiscovery.Tell(
                new ClusterActorDiscoveryMessages.MonitorActor(typeof(EchoConsumerActor).Name));

            Receive<ClusterActorDiscoveryMessages.ActorUp>(m => OnMessage(m));
            Receive<ClusterActorDiscoveryMessages.ActorDown>(m => OnMessage(m));
            Receive<string>(m => OnMessage(m));

            _clusterContext.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, "Echo", null);
        }

        private void OnMessage(ClusterActorDiscoveryMessages.ActorUp m)
        {
            Console.WriteLine("ClusterActorDiscoveryMessages.ActorUp");
            _consumers.Add(m.Actor);
        }

        private void OnMessage(ClusterActorDiscoveryMessages.ActorDown m)
        {
            Console.WriteLine("ClusterActorDiscoveryMessages.ActorDown");
            _consumers.Remove(m.Actor);
        }

        private void OnMessage(string s)
        {
            if (_consumers.Count == 0)
                return;

            _consumerCursor = (_consumerCursor + 1) % _consumers.Count;
            _consumers[_consumerCursor].Tell(s + " " + DateTime.UtcNow, Self);
        }
    }
}
