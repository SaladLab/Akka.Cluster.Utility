using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Utility;
using Akka.Configuration;

namespace BasicDistributedActor
{
    using Table = DistributedActorTable<long>;
    using TableContainer = DistributedActorTableContainer<long>;
    using Message = DistributedActorTableMessage<long>;

    class Program
    {
        private class ClusterNode
        {
            public ClusterNodeContext Context;
            public string[] Roles;
            public List<IActorRef> RootActors;
        }

        private static readonly List<ClusterNode> _clusterNodes = new List<ClusterNode>();

        static void Main(string[] args)
        {
            var commonConfig = ConfigurationFactory.ParseString(@"
                akka {
                  actor {
                    provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                  }
                  remote {
                    helios.tcp {
                      hostname = ""127.0.0.1""
                    }
                  }
                  cluster {
                    seed-nodes = [""akka.tcp://BasicCluster@127.0.0.1:3001""]
                    auto-down-unreachable-after = 30s
                  }
                }");

            var tablePort = 3000;
            var containerPort = 4000;

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                CreateClusterNode(commonConfig, ++tablePort, "table", "container");
            }
            else
            {
                CreateClusterNode(commonConfig, ++tablePort, "table");
                CreateClusterNode(commonConfig, ++containerPort, "container");
                CreateClusterNode(commonConfig, ++containerPort, "container");
            }

            // wait for stop signal

            Action printUsage = () =>
            {
                Console.WriteLine("Press command:");
                Console.WriteLine("[usage]");
                Console.WriteLine("  c [data]    create actor");
                Console.WriteLine("  g [id]      get actor with id");
                Console.WriteLine("  d [id]      delete actor with id");
                Console.WriteLine("  i           get all actors' id");
                Console.WriteLine("  cc [role]   create node (t: table, c: table-container)");
                Console.WriteLine("  ck [role]   kill node (t: table, c: table-container)");
                Console.WriteLine("  q           quit");
            };

            printUsage();
            while (true)
            {
                var line = Console.ReadLine().ToLower();
                if (line == "q")
                {
                    break;
                }
                else if (line == "c" || line.StartsWith("c "))
                {
                    var table = GetTable();
                    if (table == null)
                    {
                        Console.WriteLine("No Table");
                        return;
                    }
                    long id = 0;
                    if (line.Length > 2)
                        long.TryParse(line.Substring(2), out id);
                    var reply = table.Ask<Message.CreateReply>(
                        new Message.Create(id, new object[] { DateTime.Now.ToString() })).Result;
                    Console.WriteLine($"Result ID: {reply.Id} {reply.Actor?.Path}");
                }
                else if (line.StartsWith("g "))
                {
                    var table = GetTable();
                    if (table == null)
                    {
                        Console.WriteLine("No Table");
                        return;
                    }
                    long id;
                    long.TryParse(line.Substring(2), out id);
                    var reply = table.Ask<Message.GetReply>(new Message.Get(id)).Result;
                    if (reply.Actor != null)
                        Console.WriteLine($"Result Actor: {reply.Actor.Path}");
                    else
                        Console.WriteLine($"No actor");
                }
                else if (line.StartsWith("d "))
                {
                    var table = GetTable();
                    if (table == null)
                    {
                        Console.WriteLine("No Table");
                        return;
                    }
                    long id;
                    long.TryParse(line.Substring(2), out id);
                    var reply = table.Ask<Message.GetReply>(new Message.Get(id)).Result;
                    if (reply.Actor != null)
                    {
                        Console.WriteLine($"Try to delete actor: {reply.Actor.Path}");
                        reply.Actor.Tell(PoisonPill.Instance);
                    }
                    else
                    {
                        Console.WriteLine($"No actor");
                    }
                }
                else if (line == "i")
                {
                    var table = GetTable();
                    if (table == null)
                    {
                        Console.WriteLine("No Table");
                        return;
                    }
                    var reply = table.Ask<Message.GetIdsReply>(new Message.GetIds()).Result;
                    Console.WriteLine($"Actors: {string.Join(" ", reply.Ids.Select(x => x.ToString()))}");
                }
                else if (line.StartsWith("cc "))
                {
                    var name = line.Substring(2).Trim();
                    switch (name)
                    {
                        case "t":
                            CreateClusterNode(commonConfig, ++tablePort, "table");
                            break;
                        case "c":
                            CreateClusterNode(commonConfig, ++containerPort, "container");
                            break;
                        default:
                            Console.WriteLine("Unknown: " + name);
                            break;
                    }
                }
                else if (line.StartsWith("ck "))
                {
                    var name = line.Substring(2).Trim();
                    switch (name)
                    {
                        case "t":
                            KillOneClusterNode("table");
                            break;
                        case "c":
                            KillOneClusterNode("container");
                            break;
                        default:
                            Console.WriteLine("Unknown: " + name);
                            break;
                    }
                }
                else
                {
                    printUsage();
                }
            }

            // stop

            Console.WriteLine("Stopping table");
            foreach (var i in _clusterNodes)
            {
                if (i.Roles.Any(r => r == "table"))
                {
                    i.RootActors[0].GracefulStop(TimeSpan.FromMinutes(1),
                                                 new Message.GracefulStop(null)).Wait();
                }
            }
            Console.WriteLine("Table stoped");

            // shutdown

            _clusterNodes.Reverse();
            foreach (var cluster in _clusterNodes)
            {
                cluster.Context.System.Shutdown();
            }
        }

        private static void CreateClusterNode(Config commonConfig, int port, params string[] roles)
        {
            var config = commonConfig
                .WithFallback("akka.remote.helios.tcp.port = " + port)
                .WithFallback("akka.cluster.roles = " + "[" + string.Join(",", roles) + "]");
            var system = ActorSystem.Create("BasicCluster", config);
            var cluster = Cluster.Get(system);
            var clusterNode = new ClusterNode
            {
                Context = new ClusterNodeContext
                {
                    System = system,
                    ClusterActorDiscovery = system.ActorOf(Props.Create(() => new ClusterActorDiscovery(cluster)),
                                                           "cluster_actor_discovery")
                },
                Roles = roles
            };
            InitClusterNode(clusterNode);
            _clusterNodes.Add(clusterNode);
        }

        private static void InitClusterNode(ClusterNode node)
        {
            node.RootActors = new List<IActorRef>();
            foreach (var role in node.Roles)
            {
                IActorRef rootActor;
                switch (role)
                {
                    case "table":
                        rootActor = node.Context.System.ActorOf(Props.Create(
                            () => new Table("TestTable", node.Context.ClusterActorDiscovery,
                                            typeof(IncrementalIntegerIdGenerator), null)));
                        break;

                    case "container":
                        rootActor = node.Context.System.ActorOf(Props.Create(
                            () => new TableContainer("TestTable", node.Context.ClusterActorDiscovery,
                                                     typeof(CommonActorFactory<TestActor>), null)));
                        break;

                    default:
                        throw new InvalidOperationException("Invalid role: " + role);
                }
                node.RootActors.Add(rootActor);
            }
        }

        private static void KillOneClusterNode(string role)
        {
            var index = _clusterNodes.FindIndex(c => c.Roles.Any(r => r == role));
            if (index != -1)
            {
                var node = _clusterNodes[index];
                _clusterNodes.RemoveAt(index);
                var cluster = Cluster.Get(node.Context.System);
                cluster.Leave(cluster.SelfAddress);
                Thread.Sleep(2000);
                node.Context.System.Shutdown();

                // TODO: Gracefully leave from cluster?
            }
        }

        private static IActorRef GetTable()
        {
            var node = _clusterNodes.FirstOrDefault(c => c.Roles.Any(r => r == "table"));
            return node?.RootActors.First();
        }

        private static IActorRef GetTableContainer(int ordinal)
        {
            var nodes = _clusterNodes.Where(c => c.Roles.Any(r => r == "container")).ToList();
            if (ordinal < 0 || ordinal >= nodes.Count)
                return null;
            return nodes[ordinal].RootActors.Last();
        }
    }
}
