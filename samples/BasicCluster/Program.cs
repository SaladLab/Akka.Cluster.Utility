using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Utility;
using Akka.Configuration;

namespace BasicCluster
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

        private static void Main(string[] args)
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

            var producerPort = 3000;
            var consumerPort = 4000;

            var standAlone = args.Length > 0 && args[0] == "standalone";
            if (standAlone)
            {
                CreateClusterNode(commonConfig, ++producerPort, "producer", "consumer");
            }
            else
            {
                CreateClusterNode(commonConfig, ++producerPort, "producer");
                CreateClusterNode(commonConfig, ++consumerPort, "consumer");
                CreateClusterNode(commonConfig, ++consumerPort, "consumer");
            }

            // wait for stop signal

            Action printUsage = () =>
            {
                Console.WriteLine("Press command:");
                Console.WriteLine("[usage]");
                Console.WriteLine("  c [role]   create node (p: producer, c: consumer)");
                Console.WriteLine("  k [role]   kill node (p: producer, c: consumer)");
                Console.WriteLine("  q          quit");
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
                    var name = line.Substring(2).Trim();
                    switch (name)
                    {
                        case "p":
                            CreateClusterNode(commonConfig, ++producerPort, "producer");
                            break;
                        case "c":
                            CreateClusterNode(commonConfig, ++consumerPort, "consumer");
                            break;
                        default:
                            Console.WriteLine("Unknown: " + name);
                            break;
                    }
                }
                else if (line.StartsWith("k "))
                {
                    var name = line.Substring(2).Trim();
                    switch (name)
                    {
                        case "p":
                            KillOneClusterNode("producer");
                            break;
                        case "c":
                            KillOneClusterNode("consumer");
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
                cluster.Context.System.Terminate();
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
                    case "producer":
                        rootActor = node.Context.System.ActorOf(Props.Create(() => new EchoProducerActor(node.Context)));
                        break;

                    case "consumer":
                        rootActor = node.Context.System.ActorOf(Props.Create(() => new EchoConsumerActor(node.Context)));
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
                node.Context.System.Terminate();

                // TODO: Gracefully leave from cluster?
            }
        }
    }
}
