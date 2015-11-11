using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;

namespace Akka.Cluster.Utility
{
    public class DistributedActorDictionaryCenter : ReceiveActor
    {
        private readonly string _name;
        private readonly IActorRef _clusterActorDiscovery;
        private readonly IIdGenerator _idGenerator;
        private readonly ILoggingAdapter _log;

        private int _lastWorkNodeIndex = -1;

        private readonly List<IActorRef> _nodes = new List<IActorRef>();
        private Dictionary<object, IActorRef> _globalActorMap = new Dictionary<object, IActorRef>();

        public DistributedActorDictionaryCenter(string name, IActorRef clusterActorDiscovery,
                                                Type idGeneratorType, object[] idGeneratorInitializeArgs)
        {
            _name = name;
            _clusterActorDiscovery = clusterActorDiscovery;
            _log = Context.GetLogger();

            if (idGeneratorType != null)
            {
                _idGenerator = (IIdGenerator)Activator.CreateInstance(idGeneratorType);
                _idGenerator.Initialize(idGeneratorInitializeArgs);
            }

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => Handle(m));

            Receive<DistributedActorDictionaryMessage.Center.Add>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Remove>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Get>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.Create>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.CreateReply>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.GetOrCreate>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.GetOrCreateReply>(m => Handle(m));
            Receive<DistributedActorDictionaryMessage.Center.GetIds>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _log.Info($"DistributedActorDictionaryCenter({_name}) Start");

            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name + "Center"), Self);
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name), Self);
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

        private void Handle(ClusterActorDiscoveryMessage.ActorUp m)
        {
            _log.Info($"Node ActorUp (Actor={m.Actor.Path})");

            // TODO: Register Local
            _nodes.Add(m.Actor);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorDown m)
        {
            _log.Info($"Node ActorDown (Actor={m.Actor.Path})");

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

            workNode.Tell(new DistributedActorDictionaryMessage.Center.Create(m.Requester, id, m.Args));
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

        private void Handle(DistributedActorDictionaryMessage.Center.GetOrCreate m)
        {
            // Try to find actor

            IActorRef actor;
            if (_globalActorMap.TryGetValue(m.Id, out actor))
            {
                Sender.Tell(new DistributedActorDictionaryMessage.Center.GetOrCreateReply(m, m.Id, actor, false));
                return;
            }

            // Send "create actor" request to work node

            var workNode = DecideWorkNode();
            if (workNode == null)
            {
                Sender.Tell(new DistributedActorDictionaryMessage.Center.GetOrCreateReply(m, m.Id, null, false));
                return;
            }

            // TODO: add to creating list to handle corner-case
            //       when work node just died after sending following message

            workNode.Tell(new DistributedActorDictionaryMessage.Center.GetOrCreate(m.Requester, m.Id, m.Args));
        }

        private void Handle(DistributedActorDictionaryMessage.Center.GetOrCreateReply m)
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
                    m.Requester.Tell(new DistributedActorDictionaryMessage.GetOrCreateReply(m.Id, null, false));
                    return;
                }

                m.Requester.Tell(new DistributedActorDictionaryMessage.GetOrCreateReply(m.Id, m.Actor, true));
            }
            else
            {
                m.Requester.Tell(new DistributedActorDictionaryMessage.GetOrCreateReply(m.Id, null, false));
            }
        }

        private void Handle(DistributedActorDictionaryMessage.Center.GetIds m)
        {
            Sender.Tell(new DistributedActorDictionaryMessage.Center.GetIdsReply(
                m, _globalActorMap.Keys.ToArray()));
        }
    }
}
