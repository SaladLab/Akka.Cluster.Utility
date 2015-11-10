using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public static class DistributedActorDictionaryMessage
    {
        public class Create
        {
        }

        public class Add
        {
        }

        public class Remove
        {
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
            public class Create
            {
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

            //public class Get
            //{
            //    public object Id { get; }

            //    public Get(object id)
            //    {
            //        Id = id;
            //    }
            //}

            //public class GetReply
            //{
            //    public object Id { get; }
            //    public IActorRef Actor { get; }

            //    public GetReply(object id, IActorRef actor)
            //    {
            //        Id = id;
            //        Actor = actor;
            //    }
            //}
        }
    }
}
