using Akka.Actor;

namespace Akka.Cluster.Utility
{
    public static class ClusterActorDiscoveryMessages
    {
        // Notify other ClusterActorDiscoveries that I'm up
        public class RegisterCluster
        {
            public UniqueAddress ClusterAddress { get; }

            public RegisterCluster(UniqueAddress clusterAddress)
            {
                ClusterAddress = clusterAddress;
            }
        }

        // Notify other ClusterNodeActors that I'm down
        public class UnregisterCluster
        {
            public UniqueAddress ClusterAddress { get; }

            public UnregisterCluster(UniqueAddress clusterAddress)
            {
                ClusterAddress = clusterAddress;
            }
        }

        // Notify other ClusterNodeActors that Actor in my cluster node is up
        public class ClusterActorUp
        {
            public IActorRef Actor { get; }
            public string Tag { get; }

            public ClusterActorUp(IActorRef actor, string tag)
            {
                Actor = actor;
                Tag = tag;
            }
        }

        // Notify other ClusterNodeActors that Actor in my cluster node is down
        public class ClusterActorDown
        {
            public IActorRef Actor { get; }

            public ClusterActorDown(IActorRef actor)
            {
                Actor = actor;
            }
        }

        // Notify watcher that actor monitored is up
        public class ActorUp
        {
            public IActorRef Actor { get; }
            public string Tag { get; }

            public ActorUp(IActorRef actor, string tag)
            {
                Actor = actor;
                Tag = tag;
            }
        }

        // Notify watcher that actor monitored is down
        public class ActorDown
        {
            public IActorRef Actor { get; }
            public string Tag { get; }

            public ActorDown(IActorRef actor, string tag)
            {
                Actor = actor;
                Tag = tag;
            }
        }

        // Notify discovery actor that actor is up
        public class RegisterActor
        {
            public IActorRef Actor { get; }
            public string Tag { get; }

            public RegisterActor(IActorRef actor, string tag)
            {
                Actor = actor;
                Tag = tag;
            }
        }

        // Notify discovery actor that actor is down
        public class UnregisterActor
        {
            public IActorRef Actor { get; }

            public UnregisterActor(IActorRef actor)
            {
                Actor = actor;
            }
        }

        // Monitors actors with specific tag up or down.
        public class MonitorActor
        {
            public string Tag { get; }

            public MonitorActor(string tag)
            {
                Tag = tag;
            }
        }

        // Stops monitoring actors with specific tag up or down.
        public class UnmonitorActor
        {
            public string Tag { get; }

            public UnmonitorActor(string tag)
            {
                Tag = tag;
            }
        }
    }
}
