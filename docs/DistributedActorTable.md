## DistributedActorTable

### Examples

Let's assume that we will make chatting service. There are user actor and chatting room actor.
For scale out service cluster is built. Actors might be created in multiple cluster nodes.
In this situation we need to handle following cases.

 - A user actor can send direct message to an another user actor in different node.
 - A user actor can join a chatting room actor in different node.

Because Akka.Cluster already provides location transparency of actors, only thing
that we need to do is getting target actor reference.
`DistributedActorTable` holds id-actor reference mapping table and
you can add, get and remove actors to it.

### Figure!

 - TODO: One Table Node & Multiple Actor Worker Nodes

### Sharding

For simple implementation, `DistributedActorTable` keeps a table in one node, which
could be SPOF/B. But actors are distributed so make your service scalable.
Also table node can handle runtime add or failure of worker nodes which hold actors.

### Messages

#### Create

Actor can be created by actor table. One of worker nodes will create this actor and hold it.
You can decide ID of actor or let id-generator make it.

```csharp
var reply = table.Ask<Message.CreateReply>(
    new Message.Create(id, new object[] { DateTime.Now.ToString() })).Result;
```

#### Get

You can get actor reference from table actor by actor ID.

```csharp
var reply = table.Ask<Message.GetReply>(new Message.Get(id)).Result;
Console.WriteLine(reply.Actor);
```

#### GetOrCreate

GetOrCreate tries to get actor reference from provided ID. When it cannot find it,
it will create an actor for ID like `Create` message.

#### GetIds

GetIds provides all IDs in table. This message should be used carefully.
Because there are tons of actors in a table.
It retreive a single big message holding all IDs.

```csharp
var reply = table.Ask<Message.GetIdsReply>(new Message.GetIds()).Result;
Console.WriteLine(reply.Ids);
```

#### Add (to local worker)

Add adds an actor to table. But it's different to Create. Create message can only
be processed on a table node. But Add message can only be processed on a worker node.

#### Remove (to local worker)

Remove removes an actor from table. Like Add message, it can only be processed on a worker node.
It's quite rare to send Remove message because a worker node watchs actors that they holds
and when actor terminated, it will remove an actor reference from table automatically.
