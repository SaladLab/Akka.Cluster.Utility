using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public class DistributedActorDictionaryCenter : ReceiveActor
    {
        private readonly string _name;
        private readonly IIdGenerator _idGenerator;
        private readonly IActorRef _clusterActorDiscovery;
        
        private int _lastWorkNodeIndex = -1;

        private readonly List<IActorRef> _nodes = new List<IActorRef>();
        private Dictionary<object, IActorRef> _globalActorMap = new Dictionary<object, IActorRef>();

        public DistributedActorDictionaryCenter(string name, Type idGeneratorType, IActorRef clusterActorDiscovery)
        {
            _name = name;
            _clusterActorDiscovery = clusterActorDiscovery;

            if (idGeneratorType != null)
                _idGenerator = (IIdGenerator)Activator.CreateInstance(idGeneratorType);

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => Handle(m));

            Receive<DistributedActorDictionaryMessage.Center.Add>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Remove>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Get>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Create>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.CreateReply>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name + "Center"), Self);
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name), Self);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorUp m)
        {
            // TODO: Register Local
            _nodes.Add(m.Actor);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorDown m)
        {
            // TODO: Unregister Local
            _nodes.Remove(m.Actor);
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

            Sender.Tell(new DistributedActorDictionaryMessage.Center.AddReply(m, added));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Remove m)
        {
            // TODO: Sender check

            var removed = _globalActorMap.Remove(m.Id);

            Sender.Tell(new DistributedActorDictionaryMessage.Center.RemoveReply(m, removed));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Get m)
        {
            IActorRef actor;
            _globalActorMap.TryGetValue(m.Id, out actor);
            Sender.Tell(new DistributedActorDictionaryMessage.Center.GetReply(m, actor));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.Create m)
        {
            // decide ID (if provided, use it, otherwise generate new one)

            object id = null;
            if (m.Id != null)
            {
                if (_globalActorMap.ContainsKey(m.Id))
                {
                    Sender.Tell(new DistributedActorDictionaryMessage.Center.CreateReply(m, m.Id, null));
                    return;
                }
                id = m.Id;
            }
            else
            {
                if (_idGenerator == null)
                {
                    // TODO: Write error log

                    Sender.Tell(new DistributedActorDictionaryMessage.Center.CreateReply(m, m.Id, null));
                    return;
                }

                id = _idGenerator.GenerateId();
                if (_globalActorMap.ContainsKey(id))
                {
                    // TODO: Write error log

                    Sender.Tell(new DistributedActorDictionaryMessage.Center.CreateReply(m, m.Id, null));
                    return;
                }
            }

            // Send "create actor" request to work node

            var workNode = DecideWorkNode();
            if (workNode == null)
            {
                Sender.Tell(new DistributedActorDictionaryMessage.Center.CreateReply(m, m.Id, null));
                return;
            }

            // TODO: add to creating list to handle corner-case
            //       when work node just died after sending following message

            workNode.Tell(new DistributedActorDictionaryMessage.Center.Create(m.Requester, id, m.ActorProps));
        }

        private IActorRef DecideWorkNode()
        {
            // round-robind

            if (_nodes.Count == 0)
                return null;

            var index = _lastWorkNodeIndex + 1;
            if (index >= _nodes.Count)
                index = 0;
            _lastWorkNodeIndex = index;

            return _nodes[index];
        }

        private void Handle(DistributedActorDictionaryMessage.Center.CreateReply m)
        {
            if (m.Actor != null)
            {
                try
                {
                    _globalActorMap.Add(m.Id, m.Actor);
                }
                catch (Exception e)
                {
                    // TODO: Write error log
                    // TODO: Sender.Tell(RemoveIt)
                    m.Requester.Tell(new DistributedActorDictionaryMessage.CreateReply(m.Id, null));
                    return;
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
