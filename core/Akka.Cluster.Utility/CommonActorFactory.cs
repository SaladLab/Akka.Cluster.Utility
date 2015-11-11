using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public class CommonActorFactory<TActor> : IActorFactory
         where TActor : ActorBase
    {
        public void Initialize(object[] args)
        {
        }

        public IActorRef CreateActor(IActorRefFactory actorRefFactory, object[] args)
        {
            return actorRefFactory.ActorOf(Props.Create<TActor>(args));
        }
    }
}
