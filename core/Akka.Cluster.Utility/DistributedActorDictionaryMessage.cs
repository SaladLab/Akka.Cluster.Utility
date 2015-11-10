using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public static class DistributedActorDictionaryMessage
    {
        public class Create
        {
            public object Id { get; }
            public Props ActorProps { get; }

            public Create(Props actorProps)
            {
                ActorProps = actorProps;
            }

            public Create(object id, Props actorProps)
            {
                Id = id;
                ActorProps = actorProps;
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

        internal static class Center
        {
            public class Base
            {
                public IActorRef Requester { get; }

                public Base(IActorRef requester)
                {
                    Requester = requester;
                }

                public Base(Base requestMessage)
                {
                    Requester = requestMessage.Requester;
                }
            }

            public class Create : Base
            {
                public object Id { get; }
                public Props ActorProps { get; }

                public Create(IActorRef requester, object id, Props actorProps)
                    : base(requester)
                {
                    Id = id;
                    ActorProps = actorProps;
                }
            }

            public class CreateReply : Base
            {
                public object Id { get; }
                public IActorRef Actor { get; }

                public CreateReply(Create requestMessage, object id, IActorRef actor)
                    : base(requestMessage)
                {
                    Id = id;
                    Actor = actor;
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

                public AddReply(Add requestMessage, bool added)
                    : base(requestMessage)
                {
                    Id = requestMessage.Id;
                    Actor = requestMessage.Actor;
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

                public RemoveReply(Remove requestMessage, bool removed)
                {
                    Id = requestMessage.Id;
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

                public GetReply(Get requestMessage, IActorRef actor)
                    : base(requestMessage)
                {
                    Id = requestMessage.Id;
                    Actor = actor;
                }
            }
        }
    }
}
