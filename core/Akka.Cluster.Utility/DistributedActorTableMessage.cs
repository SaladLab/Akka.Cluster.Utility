using System;
using System.Collections.Generic;
using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public static class DistributedActorTableMessage<TKey>
    {
        public class Create
        {
            public TKey Id { get; }
            public object[] Args { get; }

            public Create(object[] args)
            {
                Args = args;
            }

            public Create(TKey id, object[] args)
            {
                Id = id;
                Args = args;
            }
        }

        public class CreateReply
        {
            public TKey Id { get; }
            public IActorRef Actor { get; }

            public CreateReply(TKey id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class GetOrCreate
        {
            public TKey Id { get; }
            public object[] Args { get; }

            public GetOrCreate(TKey id, object[] args)
            {
                Id = id;
                Args = args;
            }
        }

        public class GetOrCreateReply
        {
            public TKey Id { get; }
            public IActorRef Actor { get; }
            public bool Created { get; }

            public GetOrCreateReply(TKey id, IActorRef actor, bool created)
            {
                Id = id;
                Actor = actor;
                Created = created;
            }
        }

        public class Get
        {
            public TKey Id { get; }

            public Get(TKey id)
            {
                Id = id;
            }
        }

        public class GetReply
        {
            public TKey Id { get; }
            public IActorRef Actor { get; }

            public GetReply(TKey id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class GetIds
        {
        }

        public class GetIdsReply
        {
            public TKey[] Ids;

            public GetIdsReply(TKey[] ids)
            {
                Ids = ids;
            }
        }

        // Request to a table to stop all table & actors contained gracefully
        public class GracefulStop
        {
            public object StopMessage { get; }

            public GracefulStop(object stopMessage)
            {
                StopMessage = stopMessage;
            }
        }

        // Ask for a local container to add actor to table. (not for table directly)
        public class Add
        {
            public TKey Id { get; }
            public IActorRef Actor { get; }

            public Add(TKey id, IActorRef actor)
            {
                Id = id;
                Actor = actor;
            }
        }

        public class AddReply
        {
            public TKey Id { get; }
            public IActorRef Actor { get; }
            public bool Added { get; }

            public AddReply(TKey id, IActorRef actor, bool added)
            {
                Id = id;
                Actor = actor;
                Added = added;
            }
        }

        // Ask for a local container to remove actor to table. (not for table directly)
        public class Remove
        {
            public TKey Id { get; }

            public Remove(TKey id)
            {
                Id = id;
            }
        }

        internal static class Internal
        {
            public class Create
            {
                public TKey Id { get; }
                public object[] Args { get; }

                public Create(TKey id, object[] args)
                {
                    Id = id;
                    Args = args;
                }
            }

            public class CreateReply
            {
                public TKey Id { get; }
                public IActorRef Actor { get; }

                public CreateReply(TKey id, IActorRef actor)
                {
                    Id = id;
                    Actor = actor;
                }
            }

            public class Add
            {
                public TKey Id { get; }
                public IActorRef Actor { get; }

                public Add(TKey id, IActorRef actor)
                {
                    Id = id;
                    Actor = actor;
                }
            }

            public class AddReply
            {
                public TKey Id { get; }
                public IActorRef Actor { get; }
                public bool Added { get; }

                public AddReply(TKey id, IActorRef actor, bool added)
                {
                    Id = id;
                    Actor = actor;
                    Added = added;
                }
            }

            public class Remove
            {
                public TKey Id { get; }

                public Remove(TKey id)
                {
                    Id = id;
                }
            }

            public class GracefulStop
            {
                public object StopMessage { get; }

                public GracefulStop(object stopMessage)
                {
                    StopMessage = stopMessage;
                }
            }
        }
    }
}
