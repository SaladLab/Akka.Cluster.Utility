# Akka.Cluster.Utility

[![NuGet Status](http://img.shields.io/nuget/v/Akka.Cluster.Utility.svg?style=flat)](https://www.nuget.org/packages/Akka.Cluster.Utility/)
[![Build status](https://ci.appveyor.com/api/projects/status/7upfwl804u1m3huf/branch/master?svg=true)](https://ci.appveyor.com/project/veblush/akka-cluster-utility/branch/master)
[![Coverage Status](https://coveralls.io/repos/github/SaladLab/Akka.Cluster.Utility/badge.svg?branch=master)](https://coveralls.io/github/SaladLab/Akka.Cluster.Utility?branch=master)
[![Coverity Status](https://scan.coverity.com/projects/8459/badge.svg?flat=1)](https://scan.coverity.com/projects/saladlab-akka-cluster-utility)

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
