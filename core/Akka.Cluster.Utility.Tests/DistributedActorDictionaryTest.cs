using System;
using System.Linq;
using Akka.Actor;
using Xunit;
using Akka.TestKit.TestActors;
using Akka.TestKit;
using System.Threading.Tasks;

namespace Akka.Cluster.Utility.Tests
{
    public class DistributedActorDictionaryTest : TestKit.Xunit2.TestKit
    {
        public Tuple<TestActorRef<DistributedActorDictionaryCenter>, TestActorRef<DistributedActorDictionary>[]> 
            Setup(int nodeCount = 2)
        {
            var name = "TestDict";
            var nullDiscovery = ActorOf(BlackHoleActor.Props);
            var center = ActorOfAsTestActorRef<DistributedActorDictionaryCenter>(
                Props.Create<DistributedActorDictionaryCenter>(
                    name, typeof(IncrementalIntegerIdGenerator), nullDiscovery), name + "Center");
            var nodes = new TestActorRef<DistributedActorDictionary>[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                var node = ActorOfAsTestActorRef<DistributedActorDictionary>(
                    Props.Create<DistributedActorDictionary>(name, nullDiscovery), name + i);
                center.Tell(new ClusterActorDiscoveryMessage.ActorUp(node, name + "Center"));
                node.Tell(new ClusterActorDiscoveryMessage.ActorUp(center, name));
                nodes[i] = node;
            }
            return Tuple.Create(center, nodes);
        }

        [Fact]
        public async Task Test_Add_NonExistence_Succeed()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.AddReply>(
                new DistributedActorDictionaryMessage.Add("One", testActor));
            Assert.Equal("One", reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);
        }

        [Fact]
        public async Task Test_Add_Existence_Fail()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.AddReply>(
                new DistributedActorDictionaryMessage.Add("One", testActor));
            Assert.Equal("One", reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);

            var reply2 = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.AddReply>(
                new DistributedActorDictionaryMessage.Add("One", testActor));
            Assert.Equal("One", reply2.Id);
            Assert.Equal(testActor, reply2.Actor);
            Assert.Equal(false, reply2.Added);
        }

        [Fact]
        public async Task Test_Add_And_Get_Succeed()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);
            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("One", testActor));
            var reply = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get("One"));
            Assert.Equal("One", reply.Id);
            Assert.Equal(testActor, reply.Actor);
        }

        [Fact]
        public async Task Test_Get_NonExistence_Fail()
        {
            var actors = Setup(2);
            var reply = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get("One"));
            Assert.Equal("One", reply.Id);
            Assert.Equal(null, reply.Actor);
        }

        [Fact]
        public async Task Test_Add_And_Remove_Succeed()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);

            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("One", testActor));
            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Remove("One"));
            var reply = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get("One"));
            Assert.Equal("One", reply.Id);
            Assert.Equal(null, reply.Actor);
        }

        [Fact]
        public async Task Test_Create_And_Get_Succeed()
        {
            var actors = Setup(2);

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.CreateReply>(
                new DistributedActorDictionaryMessage.Create(BlackHoleActor.Props));
            Assert.Equal(1L, reply.Id);
            Assert.NotNull(reply.Actor);

            var reply2 = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_CreateWithId_And_Get_Succeed()
        {
            var actors = Setup(2);

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.CreateReply>(
                new DistributedActorDictionaryMessage.Create("One", BlackHoleActor.Props));
            Assert.Equal("One", reply.Id);
            Assert.NotNull(reply.Actor);

            var reply2 = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_GetOrCreate_And_Get_Succeed()
        {
            var actors = Setup(2);

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.GetOrCreateReply>(
                new DistributedActorDictionaryMessage.GetOrCreate("One", BlackHoleActor.Props));
            Assert.Equal("One", reply.Id);
            Assert.NotNull(reply.Actor);
            Assert.Equal(true, reply.Created);

            var reply2 = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_AddOne_GetOrCreate_Succeed()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);

            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("One", testActor));

            var reply = await actors.Item2[0].Ask<DistributedActorDictionaryMessage.GetOrCreateReply>(
                new DistributedActorDictionaryMessage.GetOrCreate("One", BlackHoleActor.Props));
            Assert.Equal("One", reply.Id);
            Assert.NotNull(reply.Actor);
            Assert.Equal(false, reply.Created);
        }

        [Fact]
        public async Task Test_AddTwo_GetIds_Succeed()
        {
            var actors = Setup(2);
            var testActor1 = ActorOf(BlackHoleActor.Props);
            var testActor2 = ActorOf(BlackHoleActor.Props);

            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("One", testActor1));
            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("Two", testActor2));

            var reply = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.GetIdsReply>(
                new DistributedActorDictionaryMessage.GetIds());
            Assert.Equal(new object[] { "One", "Two" }, reply.Ids);
        }

        // [Fact]
        public async Task Test_Add_And_RemoveFromOtherNode_Failed()
        {
            var actors = Setup(2);
            var testActor = ActorOf(BlackHoleActor.Props);

            actors.Item2[0].Tell(new DistributedActorDictionaryMessage.Add("One", testActor));
            actors.Item2[1].Tell(new DistributedActorDictionaryMessage.Remove("One"));
            var reply = await actors.Item2[1].Ask<DistributedActorDictionaryMessage.GetReply>(
                new DistributedActorDictionaryMessage.Get("One"));
            Assert.Equal("One", reply.Id);
            Assert.Equal(testActor, reply.Actor);
        }

        // [Fact]
        public async Task Test_Add_NodeDown_Get_Failed()
        {
            await Task.Yield();
        }
    }
}
