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

            var centerPort = 3000;
            var nodePort = 4000;

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                CreateClusterNode(commonConfig, ++centerPort, "center", "node");
            }
            else
            {
                CreateClusterNode(commonConfig, ++centerPort, "center");
                CreateClusterNode(commonConfig, ++nodePort, "node");
                CreateClusterNode(commonConfig, ++nodePort, "node");
            }

            // wait for stop signal

            Action printUsage = () =>
            {
                Console.WriteLine("Press command:");
                Console.WriteLine("[usage]");
                Console.WriteLine("  c [data]    create actor");
                Console.WriteLine("  g [id]      get actor with id");
                Console.WriteLine("  d [id]      delete actor with id");
                Console.WriteLine("  cc [role]   create node (c: center, n: dictionary-node)");
                Console.WriteLine("  ck [role]   kill node (c: center, n: dictionary-node)");
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
                else if (line.StartsWith("c "))
                {
                    var node = GetDictionaryNode(0);
                    if (node == null)
                    {
                        Console.WriteLine("No Node");
                        return;
                    }
                    long id;
                    long.TryParse(line.Substring(2), out id);
                    var reply = node.Ask<DistributedActorDictionaryMessage.CreateReply>(
                        new DistributedActorDictionaryMessage.Create(id != 0 ? (object)id : null,
                                                                     new object[] { DateTime.Now.ToString() })).Result;
                    Console.WriteLine($"Result ID: {reply.Id} {reply.Actor?.Path}");
                }
                else if (line.StartsWith("g "))
                {
                    var node = GetDictionaryNode(0);
                    if (node == null)
                    {
                        Console.WriteLine("No Node");
                        return;
                    }
                    long id;
                    long.TryParse(line.Substring(2), out id);
                    var reply = node.Ask<DistributedActorDictionaryMessage.GetReply>(
                        new DistributedActorDictionaryMessage.Get(id)).Result;
                    if (reply.Actor != null)
                        Console.WriteLine($"Result Actor: {reply.Actor.Path}");
                    else
                        Console.WriteLine($"No actor");
                }
                else if (line.StartsWith("d "))
                {
                    var node = GetDictionaryNode(0);
                    if (node == null)
                    {
                        Console.WriteLine("No Node");
                        return;
                    }
                    long id;
                    long.TryParse(line.Substring(2), out id);
                    node.Tell(new DistributedActorDictionaryMessage.Remove(id));
                }
                else if (line.StartsWith("cc "))
                {
                    var name = line.Substring(2).Trim();
                    switch (name)
                    {
                        case "c":
                            CreateClusterNode(commonConfig, ++centerPort, "center");
                            break;
                        case "n":
                            CreateClusterNode(commonConfig, ++nodePort, "node");
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
                        case "c":
                            KillOneClusterNode("center");
                            break;
                        case "n":
                            KillOneClusterNode("node");
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
                    case "center":
                        rootActor = node.Context.System.ActorOf(Props.Create(
                            () => new DistributedActorDictionaryCenter(
                                      "Test", node.Context.ClusterActorDiscovery,
                                      typeof(IncrementalIntegerIdGenerator), null)));
                        break;

                    case "node":
                        rootActor = node.Context.System.ActorOf(Props.Create(
                            () => new DistributedActorDictionary(
                                      "Test", node.Context.ClusterActorDiscovery,
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

        private static IActorRef GetDictionaryNode(int ordinal)
        {
            var nodes = _clusterNodes.Where(c => c.Roles.Any(r => r == "node")).ToList();
            if (ordinal < 0 || ordinal >= nodes.Count)
                return null;
            return nodes[ordinal].RootActors.Last();
        }
    }
}
