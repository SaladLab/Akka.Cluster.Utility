using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public interface IActorFactory
    {
        void Initialize(object[] args);
        IActorRef CreateActor(IActorRefFactory actorRefFactory, object id, object[] args);
    }
}
