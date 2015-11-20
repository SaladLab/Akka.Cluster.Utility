[![Build status](https://ci.appveyor.com/api/projects/status/7upfwl804u1m3huf/branch/master?svg=true)](https://ci.appveyor.com/project/veblush/akka-cluster-utility/branch/master)

# Akka.Cluster.Utility

This library provides small utilities that might be useful to write a program
with Akka.Cluster. Here is a summary.

 - [ClusterActorDiscovery](docs/ClusterActorDiscovery.md)
   provides a actor discovery service giving you actor references you want to discover.

 - [DistributedActorTable](docs/DistributedActorTable.md)
   provides a actor table containing actors distributed across cluster nodes.

#### Where can I get it?

```
PM> Install-Package Akka.Cluster.Utility -pre
```
