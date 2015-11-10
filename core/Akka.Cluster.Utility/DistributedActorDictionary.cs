using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public class DistributedActorDictionary : ReceiveActor
    {
        private readonly string _name;
        private readonly IActorRef _clusterActorDiscovery;
        private IActorRef _center;
        private Dictionary<object, IActorRef> _localActorMap = new Dictionary<object, IActorRef>();

        public DistributedActorDictionary(string name, IActorRef clusterActorDiscovery)
        {
            _name = name;
            _clusterActorDiscovery = clusterActorDiscovery;

            // GetOrCreate
            // Create
            // Get
            // Add
            // Remove

            Receive<DistributedActorDictionaryMessage.Add>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name), Self);
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name + "Center"), Self);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorUp m)
        {
            if (_center != null)
                return;

            _center = m.Actor;
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorDown m)
        {
            if (_center.Equals(m.Actor))
                _center = null;
        }

        // DistributedActorDictionaryMessage Messages

        private void Handle(DistributedActorDictionaryMessage.Create m)
        {
            // TODO: Send Center
        }

        private void Handle(DistributedActorDictionaryMessage.Add m)
        {
            // CHECK HERE
            // TODO: Send Center
        }

        private void Handle(DistributedActorDictionaryMessage.Remove m)
        {
            // CHECK HERE
            // TODO: Send Center
        }

        private void Handle(DistributedActorDictionaryMessage.Get m)
        {
            // Find actor in local map

            IActorRef actor;
            if (_localActorMap.TryGetValue(m.Id, out actor))
            {
                Sender.Tell(new DistributedActorDictionaryMessage.GetReply(m.Id, actor));
                return;
            }

            // When a local map does not contain a specified actor, ask for center

            if (_center == null)
            {
                Sender.Tell(new DistributedActorDictionaryMessage.GetReply(m.Id, null));
                return;
            }

            _center.Tell(new DistributedActorDictionaryMessage.Get(m.Id), Sender);
        }
    }
}
