using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.TestActors;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Cluster.Utility.Tests
{
    using Table = DistributedActorTable<long>;
    using TableContainer = DistributedActorTableContainer<long>;
    using Message = DistributedActorTableMessage<long>;

    public class DistributedActorTableContainerTest : TestKit.Xunit2.TestKit
    {
        public class TestSystem
        {
            private string _name;
            private IActorRef _nullDiscovery;
            private TestKitBase _testKit;

            public TestActorRef<Table> Table { get; private set; }
            public List<TestActorRef<TableContainer>> Containers { get; private set; }

            public TestSystem(TestKitBase testKit, string name, int containerCount)
            {
                _testKit = testKit;
                _name = name;
                _nullDiscovery = _testKit.ActorOf(BlackHoleActor.Props);

                Table = _testKit.ActorOfAsTestActorRef(
                    () => new Table("TEST", _name, _nullDiscovery, typeof(IncrementalIntegerIdGenerator), null),
                    _name);
                Containers = new List<TestActorRef<DistributedActorTableContainer<long>>>();
                for (int i = 0; i < containerCount; i++)
                    AddConstainer();
            }

            public void AddConstainer()
            {
                var index = Containers.Count;
                var node = _testKit.ActorOfAsTestActorRef(
                    () => new TableContainer(_name, _nullDiscovery, typeof(CommonActorFactory<BlackHoleActor>), null, null),
                    _name + "Container" + Containers.Count);

                node.Tell(new ClusterActorDiscoveryMessage.ActorUp(Table, _name));
                Containers.Add(node);

                Table.Tell(new ClusterActorDiscoveryMessage.ActorUp(node, _name));
            }
        }

        public DistributedActorTableContainerTest(ITestOutputHelper output)
            : base(output: output)
        {
        }

        private TestSystem Setup(int containerCount = 2)
        {
            return new TestSystem(this, "Test", containerCount);
        }

        [Fact]
        public async Task Test_Create_And_Get_Succeed()
        {
            var sys = Setup();

            var reply = await sys.Table.Ask<Message.CreateReply>(
                new Message.Create(BlackHoleActor.Props.Arguments));
            Assert.Equal(1L, reply.Id);
            Assert.NotNull(reply.Actor);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_CreateWithId_And_Get_Succeed()
        {
            var sys = Setup();

            var reply = await sys.Table.Ask<Message.CreateReply>(
                new Message.Create(10, BlackHoleActor.Props.Arguments));
            Assert.Equal(10, reply.Id);
            Assert.NotNull(reply.Actor);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_GetOrCreate_And_Get_Succeed()
        {
            var sys = Setup();

            var reply = await sys.Table.Ask<Message.GetOrCreateReply>(
                new Message.GetOrCreate(10, BlackHoleActor.Props.Arguments));
            Assert.Equal(10, reply.Id);
            Assert.NotNull(reply.Actor);
            Assert.Equal(true, reply.Created);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_Create_GetOrCreate_Succeed()
        {
            var sys = Setup();

            sys.Table.Tell(new Message.Create(10, BlackHoleActor.Props.Arguments));

            var reply = await sys.Table.Ask<Message.GetOrCreateReply>(
                new Message.GetOrCreate(10, BlackHoleActor.Props.Arguments));
            Assert.Equal(10, reply.Id);
            Assert.NotNull(reply.Actor);
            Assert.Equal(false, reply.Created);
        }

        [Fact]
        public async Task Test_GetOrCreate_GetOrCreate_Succeed()
        {
            var sys = Setup();

            var tasks = Enumerable.Range(0, 3).Select(
                _ => sys.Table.Ask<Message.GetOrCreateReply>(
                    new Message.GetOrCreate(10, BlackHoleActor.Props.Arguments)));

            var replies = await Task.WhenAll(tasks);
            for (int i = 0; i < replies.Length; i++)
            {
                var reply = replies[i];
                Assert.Equal(10, reply.Id);
                Assert.NotNull(reply.Actor);
                Assert.Equal(i == 0, reply.Created);
            }
        }

        [Fact]
        public async Task Test_Create_Timeout_Fail()
        {
            var sys = Setup();

            // stop containers from working
            sys.Containers[0].Tell(new ClusterActorDiscoveryMessage.ActorDown(sys.Table, "TestDict"));
            sys.Containers[1].Tell(new ClusterActorDiscoveryMessage.ActorDown(sys.Table, "TestDict"));

            sys.Table.Tell(new Message.Create(10, BlackHoleActor.Props.Arguments));

            var t = sys.Table.Ask<Message.CreateReply>(
                new Message.Create(10, BlackHoleActor.Props.Arguments));

            // force table to make all pending creating-actor trials expired
            sys.Table.Tell(new Table.CreateTimeoutMessage { Timeout = TimeSpan.Zero });

            var reply = await t;
            Assert.Equal(10, reply.Id);
            Assert.Null(reply.Actor);
        }

        [Fact]
        public async Task Test_CreateTwo_GetIds_Succeed()
        {
            var sys = Setup();

            sys.Table.Tell(new Message.Create(10, BlackHoleActor.Props.Arguments));
            sys.Table.Tell(new Message.Create(11, BlackHoleActor.Props.Arguments));

            var reply = await sys.Table.Ask<Message.GetIdsReply>(
                new Message.GetIds());
            Assert.Equal(new long[] { 10, 11 }, reply.Ids);
        }

        [Fact]
        public async Task Test_Add_NonExistence_Get_Succeed()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await sys.Containers[0].Ask<Message.AddReply>(new Message.Add(10, testActor));
            Assert.Equal(10, reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_Add_Existence_Fail()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await sys.Containers[0].Ask<Message.AddReply>(new Message.Add(10, testActor));
            Assert.Equal(10, reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);

            var reply2 = await sys.Containers[1].Ask<Message.AddReply>(new Message.Add(10, testActor));
            Assert.Equal(10, reply2.Id);
            Assert.Equal(testActor, reply2.Actor);
            Assert.Equal(false, reply2.Added);
        }

        [Fact]
        public async Task Test_Remove_Existence_Succeed()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            sys.Containers[0].Tell(new Message.Add(10, testActor));
            sys.Containers[0].Tell(new Message.Remove(10));
            await Task.Delay(100);

            var reply = await sys.Table.Ask<Message.GetReply>(new Message.Get(10));
            Assert.Equal(10, reply.Id);
            Assert.Null(reply.Actor);
        }

        [Fact]
        public async Task Test_Add_And_RemoveFromOtherNode_Failed()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            await sys.Containers[0].Ask<Message.AddReply>(new Message.Add(10, testActor));

            sys.Containers[1].Tell(new Message.Remove(10));
            await Task.Delay(100);

            var reply = await sys.Table.Ask<Message.GetReply>(new Message.Get(10));
            Assert.Equal(10, reply.Id);
            Assert.Equal(testActor, reply.Actor);
        }

        [Fact]
        public async Task Test_Add_And_ActorRemoved_Get_Failed()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await sys.Containers[0].Ask<Message.AddReply>(new Message.Add(10, testActor));
            Assert.Equal(10, reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);

            testActor.Tell(PoisonPill.Instance);
            await Task.Delay(100);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Null(reply2.Actor);
        }

        [Fact]
        public async Task Test_Add_And_ContainerDown_Get_Failed()
        {
            var sys = Setup();
            var testActor = ActorOf(BlackHoleActor.Props);

            var reply = await sys.Containers[0].Ask<Message.AddReply>(new Message.Add(10, testActor));
            Assert.Equal(10, reply.Id);
            Assert.Equal(testActor, reply.Actor);
            Assert.Equal(true, reply.Added);

            sys.Table.Tell(new ClusterActorDiscoveryMessage.ActorDown(sys.Containers[0], "TestDictContainer0"));
            sys.Containers[0].Tell(new ClusterActorDiscoveryMessage.ActorDown(sys.Table, "TestDict"));
            await Task.Delay(100);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Null(reply2.Actor);
        }

        [Fact]
        public async Task Test_CreateBeforeContainer_And_WhenContainerReady_Succeed()
        {
            var sys = Setup(0);

            var replyTask = sys.Table.Ask<Message.CreateReply>(
                new Message.Create(10, BlackHoleActor.Props.Arguments));

            sys.AddConstainer();

            var reply = await replyTask;
            Assert.Equal(10, reply.Id);

            var reply2 = await sys.Table.Ask<Message.GetReply>(new Message.Get(reply.Id));
            Assert.Equal(reply.Id, reply2.Id);
            Assert.Equal(reply.Actor, reply2.Actor);
        }

        [Fact]
        public async Task Test_GracefulStop_With_Container_Table()
        {
            var sys = Setup();
            var ok = await sys.Table.GracefulStop(TimeSpan.FromMinutes(1), new Message.GracefulStop(null));
            Assert.True(ok);
        }

        [Fact]
        public async Task Test_GracefulStop_With_Container_With_Actors_Table()
        {
            var sys = Setup();
            sys.Table.Tell(new Message.Create(BlackHoleActor.Props.Arguments));
            sys.Table.Tell(new Message.Create(BlackHoleActor.Props.Arguments));
            var ok = await sys.Table.GracefulStop(TimeSpan.FromMinutes(1), new Message.GracefulStop(null));
            Assert.True(ok);
        }

        [Fact]
        public async Task Test_GracefulStop_Without_Container_Table()
        {
            var sys = Setup();
            Sys.Stop(sys.Containers[0]);
            Sys.Stop(sys.Containers[1]);
            var ok = await sys.Table.GracefulStop(TimeSpan.FromMinutes(1), new Message.GracefulStop(null));
            Assert.True(ok);
        }
    }
}
