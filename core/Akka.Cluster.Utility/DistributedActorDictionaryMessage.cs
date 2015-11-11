using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public static class DistributedActorDictionaryMessage
    {
        public class Add
        {
            public object Id { get; }
            public IActorRef Actor { get; }

            public Add(object id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class AddReply
        {
            public object Id { get; }
            public IActorRef Actor { get; }
            public bool Added { get; }

            public AddReply(object id, IActorRef actor, bool added)
            {
                Id = id;
                Actor = actor;
                Added = added;
            }
        }

        public class Remove
        {
            public object Id { get; }

            public Remove(object id)
            {
                Id = id;
            }
        }

        public class Get
        {
            public object Id { get; }

            public Get(object id)
            {
                Id = id;
            }
        }

        public class GetReply
        {
            public object Id { get; }
            public IActorRef Actor { get; }

            public GetReply(object id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class Create
        {
            public object Id { get; }
            public object[] Args { get; }

            public Create(object[] args)
            {
                Args = args;
            }

            public Create(object id, object[] args)
            {
                Id = id;
                Args = args;
            }
        }

        public class CreateReply
        {
            public object Id { get; }
            public IActorRef Actor { get; }

            public CreateReply(object id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class GetOrCreate
        {
            public object Id { get; }
            public object[] Args { get; }

            public GetOrCreate(object id, object[] args)
            {
                Id = id;
                Args = args;
            }
        }

        public class GetOrCreateReply
        {
            public object Id { get; }
            public IActorRef Actor { get; }
            public bool Created { get; }

            public GetOrCreateReply(object id, IActorRef actor, bool created)
            {
                Id = id;
                Actor = actor;
                Created = created;
            }
        }

        public class GetIds
        {
        }

        public class GetIdsReply
        {
            public object[] Ids;

            public GetIdsReply(object[] ids)
            {
                Ids = ids;
            }
        }

        public class ShutdownNode
        {
            public object ActorShutdownMessage;

            public ShutdownNode(object actorShutdownMessage)
            {
                ActorShutdownMessage = actorShutdownMessage;
            }
        }

        internal static class Center
        {
            public class Base
            {
                public IActorRef Requester { get; }

                public Base(IActorRef requester)
                {
                    Requester = requester;
                }
            }

            public class Add : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }

                public Add(IActorRef requester, object id, IActorRef actor)
                    : base(requester)
                {
                    Id = id;
                    Actor = actor;
                }
            }

            public class AddReply : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }
                public bool Added { get; }

                public AddReply(IActorRef requester, object id, IActorRef actor, bool added)
                    : base(requester)
                {
                    Id = id;
                    Actor = actor;
                    Added = added;
                }
            }

            public class Remove
            {
                public object Id { get; }

                public Remove(object id)
                {
                    Id = id;
                }
            }

            public class RemoveReply
            {
                public object Id { get; }
                public bool Removed { get; }

                public RemoveReply(object id, bool removed)
                {
                    Id = id;
                    Removed = removed;
                }
            }

            public class Get : Base
            {
                public object Id { get; }

                public Get(IActorRef requester, object id)
                    : base(requester)
                {
                    Id = id;
                }
            }

            public class GetReply : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }

                public GetReply(IActorRef requester, object id, IActorRef actor)
                    : base(requester)
                {
                    Id = id;
                    Actor = actor;
                }
            }

            public class Create : Base
            {
                public object Id { get; }
                public object[] Args { get; }

                public Create(IActorRef requester, object id, object[] args)
                    : base(requester)
                {
                    Id = id;
                    Args = args;
                }
            }

            public class CreateReply : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }

                public CreateReply(IActorRef requester, object id, IActorRef actor)
                    : base(requester)
                {
                    Id = id;
                    Actor = actor;
                }
            }

            public class GetOrCreate : Base
            {
                public object Id { get; }
                public object[] Args { get; }

                public GetOrCreate(IActorRef requester, object id, object[] args)
                    : base(requester)
                {
                    Id = id;
                    Args = args;
                }
            }

            public class GetOrCreateReply : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }
                public bool Created { get; }

                public GetOrCreateReply(IActorRef requester, object id, IActorRef actor, bool created)
                    : base(requester)
                {
                    Id = id;
                    Actor = actor;
                    Created = created;
                }
            }

            public class GetIds : Base
            {
                public GetIds(IActorRef requester)
                    : base(requester)
                {
                }
            }

            public class GetIdsReply : Base
            {
                public object[] Ids;

                public GetIdsReply(IActorRef requester, object[] ids)
                    : base(requester)
                {
                    Ids = ids;
                }
            }
        }
    }
}
