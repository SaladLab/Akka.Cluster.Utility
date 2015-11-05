[![Build status](https://ci.appveyor.com/api/projects/status/7upfwl804u1m3huf/branch/master?svg=true)](https://ci.appveyor.com/project/veblush/akka-cluster-utility/branch/master)

# Akka.Cluster.Utility

This library provides small utilities that might be useful to write a program
with Akka.Cluster. Here is a summary.

 - `ClusterActorDiscovery`
   provides a discovery service that you can get actor references you want to find.
 - `ShardedDictionary`
   provides a shared dictionary across cluster nodes. (NOT-YET-IMPLEMENTED)
 - `Singleton`
   provides a singleton.(NOT-YET-IMPLEMENTED)

#### Where can I get it?

```
PM> Install-Package Akka.Cluster.Utility -pre
```

## ClusterActorDiscovery

### Examples

We'll build a clustered service consisting of N echo producers and M echo consumers.
Each producer will send an echo message to a consumer every seconds.
But it need to know references of consumer actors to send a message.
To solve this problem, we can use `ClusterActorDiscovery` to discover where they are.

#### Setup

Every ActorSystem should have 1 `ClusterActorDiscovery` actor. It will keep running until
cluster shutdown. It will be used for monitoring registered actors up and down and
propagate information to other `ClusterActorDiscovery`s in cluster.

```csharp
var system = ActorSystem.Create("BasicCluster");
var cluster = Cluster.Get(system);
var clusterNode = new ClusterNode
{
    Context = new ClusterNodeContext
    {
        System = system,
        ClusterActorDiscovery = system.ActorOf(
            Props.Create(() => new ClusterActorDiscovery(cluster)),
            "cluster_actor_discovery")
    }
};
```

#### Consumer

Easy one first. Consumer class is quite simple to write.
It just receives echo messages from producer and write them.
But it has to tell `ClusterActorDiscovery` that it is created on PreStart for producer actors
to know the existence of consumer.

```csharp
class EchoConsumerActor : ReceiveActor
{
    ClusterNodeContext _clusterContext;
    UniqueAddress _clusterUniqueAddress;

    public EchoConsumerActor(ClusterNodeContext context)
    {
        _clusterContext = context;
        _clusterUniqueAddress = Cluster.Get(Context.System).SelfUniqueAddress;

        Receive<string>(m => OnMessage(m));
    }

    protected override void PreStart()
    {
        _clusterContext.ClusterActorDiscovery.Tell(
            new ClusterActorDiscoveryMessages.RegisterActor(Self, nameof(EchoConsumerActor)));
    }

    private void OnMessage(string s)
    {
        Console.WriteLine($"EchoConsumerActor({_clusterUniqueAddress.Address.Port}): {s}");
    }
}
```

#### Producer

Basically a producer sends echo messages to consumers. But it needs to get references
of consumer actors. Right after producer is created, it requests `ClusterActorDiscovery ` to
notice change of consumer actors. `ClusterActorDiscovery` will send ActorUp and ActorDown
notification messages when something happens.

```csharp
class EchoProducerActor : ReceiveActor
{
    ClusterNodeContext _clusterContext;
    List<IActorRef> _consumers = new List<IActorRef>();
    int _consumerCursor = 0;

    public EchoProducerActor(ClusterNodeContext context)
    {
        _clusterContext = context;

        Receive<ClusterActorDiscoveryMessages.ActorUp>(m => OnMessage(m));
        Receive<ClusterActorDiscoveryMessages.ActorDown>(m => OnMessage(m));
        Receive<string>(m => OnMessage(m));
    }

    protected override void PreStart()
    {
        _clusterContext.ClusterActorDiscovery.Tell(
            new ClusterActorDiscoveryMessages.MonitorActor(nameof(EchoConsumerActor)));
        _clusterContext.System.Scheduler.ScheduleTellRepeatedly(
            TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, "Echo", null);
    }

    private void OnMessage(ClusterActorDiscoveryMessages.ActorUp m)
    {
        _consumers.Add(m.Actor);
    }

    private void OnMessage(ClusterActorDiscoveryMessages.ActorDown m)
    {
        _consumers.Remove(m.Actor);
    }

    private void OnMessage(string s)
    {
        if (_consumers.Count == 0) return;

        _consumerCursor = (_consumerCursor + 1) % _consumers.Count;
        _consumers[_consumerCursor].Tell(s + " " + DateTime.UtcNow, Self);
    }
}
```

#### Output

First time there are 1 producer and 2 consumers.
```
EchoConsumerActor(4001): Echo 2015-11-04 12:55:08
EchoConsumerActor(4002): Echo 2015-11-04 12:55:09
...
```

After third consumer is added. New 4003.
```
EchoConsumerActor(4001): Echo 2015-11-04 12:55:32
EchoConsumerActor(4002): Echo 2015-11-04 12:55:33
EchoConsumerActor(4003): Echo 2015-11-04 12:55:34
...
```

After first consumer is removed. No more 4001.
```
EchoConsumerActor(4002): Echo 2015-11-04 12:55:49
EchoConsumerActor(4003): Echo 2015-11-04 12:55:50
...
```

### Things worth mentioning

 - `ClusterActorDiscovery` watches registered actors and after it gets Terminated
   message from killed actor it will automatically generate an ActorDown event
   for that.

 - Just for this example, it's better to use akka router which is robust and simple.
   But `ClusterActorDiscovery` can handle more complex cases. (sorry for poor example)

 - Tag might be used like ID or filter for registering and discovering actors.
