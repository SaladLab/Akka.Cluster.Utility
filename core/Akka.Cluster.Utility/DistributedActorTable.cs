using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;

namespace Akka.Cluster.Utility
{
    public class DistributedActorTable<TKey> : ReceiveActor
    {
        private readonly string _name;
        private readonly IActorRef _clusterActorDiscovery;
        private readonly IIdGenerator<TKey> _idGenerator;
        private readonly ILoggingAdapter _log;

        private readonly Dictionary<TKey, IActorRef> _actorMap = new Dictionary<TKey, IActorRef>();

        private class Container
        {
            public DateTime LinkTime;
            public Dictionary<TKey, IActorRef> ActorMap;
        }

        private readonly Dictionary<IActorRef, Container> _containerMap = new Dictionary<IActorRef, Container>();
        private List<IActorRef> _containerWorkQueue;
        private int _lastWorkNodeIndex = -1;

        private enum RequestType
        {
            Create,
            GetOrCreate,
            Get,
        }

        private class Creating
        {
            public DateTime RequestTime;
            public List<Tuple<IActorRef, RequestType>> Requesters;
            public IActorRef WorkingContainer;
        }

        private readonly Dictionary<TKey, Creating> _creatingMap = new Dictionary<TKey, Creating>();
        private readonly TimeSpan _createTimeout = TimeSpan.FromSeconds(10);

        public DistributedActorTable(string name, IActorRef clusterActorDiscovery,
                                     Type idGeneratorType, object[] idGeneratorInitializeArgs)
        {
            _name = name;
            _clusterActorDiscovery = clusterActorDiscovery;
            _log = Context.GetLogger();

            if (idGeneratorType != null)
            {
                try
                {
                    _idGenerator = (IIdGenerator<TKey>)Activator.CreateInstance(idGeneratorType);
                    _idGenerator.Initialize(idGeneratorInitializeArgs);
                }
                catch (Exception e)
                {
                    _log.Error(e, $"Exception in initializing ${idGeneratorType.FullName}");
                    _idGenerator = null;
                }
            }

            Receive<ClusterActorDiscoveryMessage.ActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessage.ActorDown>(m => Handle(m));

            Receive<DistributedActorTableMessage<TKey>.Create>(m => Handle(m));
            Receive<DistributedActorTableMessage<TKey>.GetOrCreate>(m => Handle(m));
            Receive<DistributedActorTableMessage<TKey>.Get>(m => Handle(m));
            Receive<DistributedActorTableMessage<TKey>.GetIds>(m => Handle(m));

            Receive<DistributedActorTableMessage<TKey>.Internal.CreateReply>(m => Handle(m));
            Receive<DistributedActorTableMessage<TKey>.Internal.Add>(m => Handle(m));
            Receive<DistributedActorTableMessage<TKey>.Internal.Remove>(m => Handle(m));

            Receive<CreateTimeoutMessage>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _log.Info($"DistributedActorTable({_name}) Start");

            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.RegisterActor(Self, _name));
            _clusterActorDiscovery.Tell(new ClusterActorDiscoveryMessage.MonitorActor(_name + "Container"));

            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), Self, new CreateTimeoutMessage(), Self);
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorUp m)
        {
            _log.Info($"Container.ActorUp (Actor={m.Actor.Path})");

            if (_containerMap.ContainsKey(m.Actor))
            {
                _log.Error($"I already have that container. (Actor={m.Actor.Path})");
                return;
            }

            _containerMap.Add(m.Actor, new Container
            {
                LinkTime = DateTime.UtcNow,
                ActorMap = new Dictionary<TKey, IActorRef>()
            });
            RebuildContainerWorkQueue();
        }

        private void Handle(ClusterActorDiscoveryMessage.ActorDown m)
        {
            _log.Info($"Container.ActorDown (Actor={m.Actor.Path})");

            Container container;
            if (_containerMap.TryGetValue(m.Actor, out container) == false)
            {
                _log.Error($"I don't have that container. (Actor={m.Actor.Path})");
                return;
            }

            _containerMap.Remove(m.Actor);
            RebuildContainerWorkQueue();

            // Remove all actors owned by this container

            foreach (var i in container.ActorMap)
            {
                _actorMap.Remove(i.Key);
                if (i.Value == null)
                {
                    // cancel all pending creating requests

                    Creating creating;
                    if (_creatingMap.TryGetValue(i.Key, out creating))
                    {
                        _creatingMap.Remove(i.Key);

                        foreach (var r in creating.Requesters)
                            r.Item1.Tell(CreateReplyMessage(r.Item2, i.Key, null, false));
                    }
                }
            }
        }

        private IActorRef DecideWorkingContainer()
        {
            // round-robind

            if (_containerWorkQueue == null || _containerWorkQueue.Count == 0)
                return null;

            var index = _lastWorkNodeIndex + 1;
            if (index >= _containerWorkQueue.Count)
                index = 0;
            _lastWorkNodeIndex = index;

            return _containerWorkQueue[index];
        }

        private void RebuildContainerWorkQueue()
        {
            _containerWorkQueue = _containerMap.Keys.ToList();
        }

        private void CreateActor(RequestType requestType, TKey id, object[] args)
        {
            // Send "create actor" request to container

            var container = DecideWorkingContainer();
            if (container == null)
            {
                Sender.Tell(CreateReplyMessage(requestType, id, null, false));
                return;
            }

            _actorMap.Add(id, null);
            _containerMap[container].ActorMap.Add(id, null);

            _creatingMap.Add(id, new Creating
            {
                RequestTime = DateTime.UtcNow,
                Requesters = new List<Tuple<IActorRef, RequestType>> { Tuple.Create(Sender, requestType) },
                WorkingContainer = container,
            });

            container.Tell(new DistributedActorTableMessage<TKey>.Internal.Create(id, args));
        }

        private object CreateReplyMessage(RequestType requestType, TKey id, IActorRef actor, bool created)
        {
            switch (requestType)
            {
                case RequestType.Create:
                    return new DistributedActorTableMessage<TKey>.CreateReply(id, actor);
                    
                case RequestType.GetOrCreate:
                    return new DistributedActorTableMessage<TKey>.GetOrCreateReply(id, actor, created);
                    
                case RequestType.Get:
                    return new DistributedActorTableMessage<TKey>.GetReply(id, actor);

                default:
                    throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
            }
        }

        private void PutOnCreateWaitingList(RequestType requestType, TKey id, IActorRef requester)
        {
            var fi = _creatingMap.FirstOrDefault(i => i.Key.Equals(id));
            if (fi.Value == null)
            {
                _log.Error($"Cannot find creatingMap. (Id=${id} RequestType={requestType})");
                Sender.Tell(CreateReplyMessage(requestType, id, null, false));
                return;
            }

            fi.Value.Requesters.Add(Tuple.Create(requester, requestType));
        }

        private void Handle(DistributedActorTableMessage<TKey>.Create m)
        {
            // decide ID (if provided, use it, otherwise generate new one)

            TKey id;
            if (m.Id.Equals(default(TKey)) == false)
            {
                if (_actorMap.ContainsKey(m.Id))
                {
                    Sender.Tell(new DistributedActorTableMessage<TKey>.CreateReply(m.Id, null));
                    return;
                }
                id = m.Id;
            }
            else
            {
                if (_idGenerator == null)
                {
                    _log.Error("I don't have ID Generator.");
                    Sender.Tell(new DistributedActorTableMessage<TKey>.CreateReply(m.Id, null));
                    return;
                }

                id = _idGenerator.GenerateId();
                if (_actorMap.ContainsKey(id))
                {
                    _log.Error($"ID generated by generator is duplicated. ID={id}, Actor={_actorMap[id]}");
                    Sender.Tell(new DistributedActorTableMessage<TKey>.CreateReply(m.Id, null));
                    return;
                }
            }

            CreateActor(RequestType.Create, id, m.Args);
        }

        private void Handle(DistributedActorTableMessage<TKey>.GetOrCreate m)
        {
            var id = m.Id;

            // try to get actor

            IActorRef actor;
            if (_actorMap.TryGetValue(id, out actor))
            {
                if (actor != null)
                {
                    Sender.Tell(new DistributedActorTableMessage<TKey>.GetOrCreateReply(m.Id, actor, false));
                    return;
                }
                else
                {
                    PutOnCreateWaitingList(RequestType.GetOrCreate, id, Sender);
                    return;
                }
            }

            CreateActor(RequestType.GetOrCreate, id, m.Args);
        }

        private void Handle(DistributedActorTableMessage<TKey>.Get m)
        {
            var id = m.Id;

            // try to get actor

            IActorRef actor;
            if (_actorMap.TryGetValue(id, out actor))
            {
                if (actor != null)
                {
                    Sender.Tell(new DistributedActorTableMessage<TKey>.GetReply(m.Id, actor));
                    return;
                }
                else
                {
                    PutOnCreateWaitingList(RequestType.GetOrCreate, id, Sender);
                    return;
                }
            }

            Sender.Tell(new DistributedActorTableMessage<TKey>.GetReply(m.Id, null));
        }

        private void Handle(DistributedActorTableMessage<TKey>.GetIds m)
        {
            Sender.Tell(new DistributedActorTableMessage<TKey>.GetIdsReply(_actorMap.Keys.ToArray()));
        }

        private void Handle(DistributedActorTableMessage<TKey>.Internal.CreateReply m)
        {
            var id = m.Id;

            IActorRef actor;
            if (_actorMap.TryGetValue(id, out actor) == false)
            {
                // request was already expired
                return;
            }

            if (actor != null)
            {
                _log.Error($"I got CreateReply but already have an actor. " + $"(Id={id} Actor={actor} ArrivedActor={m.Actor})");
                return;
            }

            Creating creating;
            if (_creatingMap.TryGetValue(id, out creating) == false)
            {
                _log.Error($"I got CreateReply but I don't have a creating. " + $"(Id={id} Actor={actor})");
                return;
            }

            // update created actor in map

            _creatingMap.Remove(id);
            _actorMap[id] = m.Actor;
            _containerMap[creating.WorkingContainer].ActorMap[id] = m.Actor;

            // send reply to requesters

            for (var i = 0; i < creating.Requesters.Count; i++)
            {
                var requester = creating.Requesters[i];
                requester.Item1.Tell(CreateReplyMessage(requester.Item2, id, m.Actor, i == 0));
            }
        }

        private void Handle(DistributedActorTableMessage<TKey>.Internal.Add m)
        {
            if (m.Actor == null)
            {
                _log.Error($"Invalid null actor for adding. (Id={m.Id})");
                Sender.Tell(new DistributedActorTableMessage<TKey>.Internal.AddReply(m.Id, m.Actor, false));
                return;
            }

            Container container;
            if (_containerMap.TryGetValue(Sender, out container) == false)
            {
                _log.Error($"Cannot find a container trying to add an actor. (Id={m.Id} Container={Sender})");
                return;
            }

            try
            {
                _actorMap.Add(m.Id, m.Actor);
                container.ActorMap.Add(m.Id, m.Actor);
            }
            catch (Exception e)
            {
                Sender.Tell(new DistributedActorTableMessage<TKey>.Internal.AddReply(m.Id, m.Actor, false));
            }
            
            Sender.Tell(new DistributedActorTableMessage<TKey>.Internal.AddReply(m.Id, m.Actor, true));
        }

        private void Handle(DistributedActorTableMessage<TKey>.Internal.Remove m)
        {
            IActorRef actor;
            if (_actorMap.TryGetValue(m.Id, out actor) == false)
                return;

            if (actor == null)
            {
                _log.Error($"Cannot remove an actor waiting for creating. (Id={m.Id})");
                return;
            }

            Container container;
            if (_containerMap.TryGetValue(Sender, out container) == false)
            {
                _log.Error($"Cannot find a container trying to remove an actor. (Id={m.Id} Container={Sender})");
                return;
            }

            if (container.ActorMap.ContainsKey(m.Id) == false)
            {
                _log.Error($"Cannot remove an actor owned by another container. (Id={m.Id} Container={Sender})");
                return;
            }

            _actorMap.Remove(m.Id);
            container.ActorMap.Remove(m.Id);
        }

        internal class CreateTimeoutMessage
        {
            public TimeSpan? Timeout;
        }

        private void Handle(CreateTimeoutMessage m)
        {
            var threshold = DateTime.UtcNow - (m.Timeout ??_createTimeout);

            var expiredItems = _creatingMap.Where(i => i.Value.RequestTime <= threshold).ToList();
            foreach (var i in expiredItems)
            {
                var id = i.Key;
                var container = i.Value.WorkingContainer;

                _log.Info($"CreateTimeout Id={id} Container={container}");

                // remove pending item

                _creatingMap.Remove(i.Key);
                _actorMap.Remove(id);
                _containerMap[container].ActorMap.Remove(id);

                // send reply to requester

                foreach (var r in i.Value.Requesters)
                    r.Item1.Tell(CreateReplyMessage(r.Item2, id, null, false));
            }
        }
    }
}
