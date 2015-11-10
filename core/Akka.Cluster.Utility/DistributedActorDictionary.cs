using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    // TODO: LOG (INFO,ERROR)
    // TODO: RequestId for disconnecting from center
    
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

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => Handle(m));

            Receive<DistributedActorDictionaryMessage.Add>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.AddReply>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Remove>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.RemoveReply>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Get>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.GetReply>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Create>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Create>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.CreateReply>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name), Self);
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name + "Center"), Self);
        }

        // ClusterActorDiscoveryMessage Message

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

        private void Handle(DistributedActorDictionaryMessage.Add m)
        {
            if (_center == null)
            {
                Sender.Tell(new DistributedActorDictionaryMessage.AddReply(m.Id, m.Actor, false));
                return;
            }

            _center.Tell(new DistributedActorDictionaryMessage.Center.Add(Sender, m.Id, m.Actor));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.AddReply m)
        {
            if (m.Added)
            {
                try
                {
                    _localActorMap.Add(m.Id, m.Actor);
                }
                catch (Exception e)
                {
                    // TODO: Write log
                }
                m.Requester.Tell(new DistributedActorDictionaryMessage.AddReply(m.Id, m.Actor, true));
            }
            else
            {
                m.Requester.Tell(new DistributedActorDictionaryMessage.AddReply(m.Id, m.Actor, false));
            }
        }

        private void Handle(DistributedActorDictionaryMessage.Remove m)
        {
            if (_center == null)
            {
                // TODO: We need to pend all remove requests?
                return;
            }

            _center.Tell(new DistributedActorDictionaryMessage.Center.Remove(m.Id));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.RemoveReply m)
        {
            if (m.Removed)
            {
                try
                {
                    if (_localActorMap.Remove(m.Id) == false)
                    {
                        // TODO: Write log for already removed ?
                    }
                }
                catch (Exception e)
                {
                    // TODO: Write log
                }
            }
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

            _center.Tell(new DistributedActorDictionaryMessage.Center.Get(Sender, m.Id));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.GetReply m)
        {
            // NOTE: If we need caching the result, this place is first area to be considered.

            m.Requester.Tell(new DistributedActorDictionaryMessage.GetReply(m.Id, m.Actor));
        }

        private void Handle(DistributedActorDictionaryMessage.Create m)
        {
            if (_center == null)
            {
                Sender.Tell(new DistributedActorDictionaryMessage.CreateReply(m.Id, null));
                return;
            }

            _center.Tell(new DistributedActorDictionaryMessage.Center.Create(Sender, m.Id, m.ActorProps));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Create m)
        {
            if (_center == null)
                return;

            var actor = Context.ActorOf(m.ActorProps, m.Id.ToString());
            _center.Tell(new DistributedActorDictionaryMessage.Center.CreateReply(m, m.Id, actor));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.CreateReply m)
        {
            if (m.Actor != null)
            {
                try
                {
                    _localActorMap.Add(m.Id, m.Actor);
                }
                catch (Exception e)
                {
                    // TODO: Write log
                }
                m.Requester.Tell(new DistributedActorDictionaryMessage.CreateReply(m.Id, m.Actor));
            }
            else
            {
                m.Requester.Tell(new DistributedActorDictionaryMessage.CreateReply(m.Id, null));
            }
        }
    }
}
