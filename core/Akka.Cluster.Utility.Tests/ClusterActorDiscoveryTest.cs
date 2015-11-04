using System;
using System.Linq;
using Akka.Actor;
using Xunit;

namespace Akka.Cluster.Utility.Tests
{
    public class ClusterActorDiscoveryTest : TestKit.Xunit2.TestKit
    {
        private UniqueAddress CreateUniqueAddress(int port)
        {
            return new UniqueAddress(new Address("protocol", "system", "localhost", port), 1);
        }

        [Fact]
        public void RegisterCluster_Register_Succeed()
        {
            var actor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            actor.Tell(new ClusterActorDiscoveryMessages.RegisterCluster(CreateUniqueAddress(1)));
        }

        [Fact]
        public void RegisterCluster_RegisterDuplicate_Fail()
        {
            var actor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            actor.Tell(new ClusterActorDiscoveryMessages.RegisterCluster(CreateUniqueAddress(1)));
            actor.Tell(new ClusterActorDiscoveryMessages.RegisterCluster(CreateUniqueAddress(1)));
        }

        [Fact]
        public void MonitorActor_SubjectActorUp_Monitored()
        {
            var discoveryActor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.MonitorActor("test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.RegisterActor(TestActor, "test"));
            ExpectMsg<ClusterActorDiscoveryMessages.ActorUp>();
        }

        [Fact]
        public void MonitorActor_SubjectActorDown_Monitored()
        {
            var discoveryActor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            var sourceActor = TestActor;
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.MonitorActor("test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.RegisterActor(sourceActor, "test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.UnregisterActor(sourceActor));
            ExpectMsg<ClusterActorDiscoveryMessages.ActorUp>();
            ExpectMsg<ClusterActorDiscoveryMessages.ActorDown>();
        }

        [Fact]
        public void MonitorActor_SubjectActorUp_NoTarget_Silient()
        {
            var discoveryActor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.MonitorActor("test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.RegisterActor(TestActor, "no"));
            ExpectNoMsg();
        }

        [Fact]
        public void MonitorActor_SubjectActorUp_AlreadyRegisterd_Monitored()
        {
            var discoveryActor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.RegisterActor(TestActor, "test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.MonitorActor("test"));
            ExpectMsg<ClusterActorDiscoveryMessages.ActorUp>();
        }

        [Fact]
        public void MonitorActor_SubjectActorDown_EvenWhenKilled_Monitored()
        {
            var discoveryActor = ActorOf(Props.Create(() => new ClusterActorDiscovery(null)));
            var testActor = CreateTestActor("will_go_away");
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.RegisterActor(testActor, "test"));
            discoveryActor.Tell(new ClusterActorDiscoveryMessages.MonitorActor("test"));
            ExpectMsg<ClusterActorDiscoveryMessages.ActorUp>();
            testActor.Tell(PoisonPill.Instance);
            ExpectMsg<ClusterActorDiscoveryMessages.ActorDown>();
        }
    }
}
