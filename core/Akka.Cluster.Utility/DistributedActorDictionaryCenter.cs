using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public class DistributedActorDictionaryCenter : ReceiveActor
    {
        private readonly string _name;
        private readonly IActorRef _clusterActorDiscovery;
        
        private int _lastWorkIndex = -1;

        private readonly List<IActorRef> _nodeDictionaries = new List<IActorRef>();
        private Dictionary<object, IActorRef> _globalActorMap = new Dictionary<object, IActorRef>();

        public DistributedActorDictionaryCenter(string name, IActorRef clusterActorDiscovery)
        {
            // GetOrCreate
            // Create
            // Get
            // Add
            // Remove

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => Handle(m));

            Receive<DistributedActorDictionaryMessage.Center.Create>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Add>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Remove>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Get>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name + "Center"), Self);
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name), Self);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorUp m)
        {
            // TODO: Register Local
            _nodeDictionaries.Add(m.Actor);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorDown m)
        {
            // TODO: Unregister Local
            _nodeDictionaries.Remove(m.Actor);
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Create m)
        {
            // Check exists and issue id and create request();
            // 
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Add m)
        {
            // TODO: Sender check

            var added = true;
            try
            {
                // TODO: added to worker set
                _globalActorMap.Add(m.Id, m.Actor);
            }
            catch (Exception e)
            {
                added = false;
            }

            Sender.Tell(new DistributedActorDictionaryMessage.Center.AddReply(m.Id, m.Actor, added));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Remove m)
        {
            // TODO: Sender check

            _globalActorMap.Remove(m.Id);
        }

        private void Handle(DistributedActorDictionaryMessage.Get m)
        {
            IActorRef actor;
            _globalActorMap.TryGetValue(m.Id, out actor);
            Sender.Tell(new DistributedActorDictionaryMessage.GetReply(m.Id, actor));
        }
    }
}
