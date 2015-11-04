using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;

namespace Akka.Cluster.Utility
{
    public class ClusterActorDiscovery : ReceiveActor
    {
        private readonly Cluster _cluster;
        private readonly ILoggingAdapter _log;
        private readonly string _name;

        // Per cluster-node data

        private class NodeItem
        {
            public UniqueAddress ClusterAddress;
            public List<ActorItem> ActorItems;
        }

        private readonly Dictionary<IActorRef, NodeItem> _nodeMap = new Dictionary<IActorRef, NodeItem>();

        // Actors in cluster

        private class ActorItem
        {
            public IActorRef Actor;
            public string Tag;
        }

        private readonly List<ActorItem> _actorItems = new List<ActorItem>();

        // Monitor items registered in this discovery actor

        private class MonitorItem
        {
            public IActorRef Actor;
            public string Tag;
        }

        private readonly List<MonitorItem> _monitorItems = new List<MonitorItem>();

        // Watching actors

        private readonly Dictionary<IActorRef, int[]> _actorWatchCountMap = new Dictionary<IActorRef, int[]>();

        public ClusterActorDiscovery(Cluster cluster)
        {
            _cluster = cluster;
            _name = Self.Path.Name;
            _log = Context.GetLogger();

            Receive<ClusterEvent.MemberUp>(m => Handle(m));
            Receive<ClusterEvent.UnreachableMember>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.RegisterCluster>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.UnregisterCluster>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.ClusterActorUp>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.ClusterActorDown>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.RegisterActor>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.UnregisterActor>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.MonitorActor>(m => Handle(m));
            Receive<ClusterActorDiscoveryMessages.UnmonitorActor>(m => Handle(m));
            Receive<Terminated>(m => Handle(m));
        }

        protected override void PreStart()
        {
            _cluster?.Subscribe(Self, new[]
            {
                typeof(ClusterEvent.MemberUp),
                typeof(ClusterEvent.UnreachableMember)
            });
        }

        protected override void PostStop()
        {
            _cluster?.Unsubscribe(Self);
        }

        private void Handle(ClusterEvent.MemberUp m)
        {
            if (_cluster != null)
            {
                if (_cluster.SelfUniqueAddress == m.Member.UniqueAddress)
                {
                    var roles = string.Join(", ", _cluster.SelfRoles);
                    _log.Info($"Cluster.Up: Address={_cluster.SelfUniqueAddress} Role={roles}");
                }
                else
                {
                    var remoteDiscoveryActor = Context.ActorSelection(m.Member.Address + "/user/" + _name);
                    remoteDiscoveryActor.Tell(
                        new ClusterActorDiscoveryMessages.RegisterCluster(_cluster.SelfUniqueAddress));

                    // Notify my actors up to registered node

                    foreach (var a in _actorItems)
                        remoteDiscoveryActor.Tell(new ClusterActorDiscoveryMessages.ClusterActorUp(a.Actor, a.Tag), Self);
                }
            }
        }

        private void Handle(ClusterEvent.UnreachableMember m)
        {
            _log.Info($"Cluster.Unreachable: Address={m.Member.Address} Role={string.Join(",", m.Member.Roles)}");

            var item = _nodeMap.FirstOrDefault(i => i.Value.ClusterAddress == m.Member.UniqueAddress);
            if (item.Key != null)
                RemoveNode(item.Key);
        }

        private void Handle(ClusterActorDiscoveryMessages.RegisterCluster m)
        {
            _log.Info($"RegisterCluster: Address={m.ClusterAddress}");

            // Register node

            var item = _nodeMap.FirstOrDefault(i => i.Value.ClusterAddress == m.ClusterAddress);
            if (item.Key != null)
            {
                _log.Error($"Already registered node. Address={m.ClusterAddress}");
                return;
            }

            _nodeMap.Add(Sender, new NodeItem
            {
                ClusterAddress = m.ClusterAddress,
                ActorItems = new List<ActorItem>()
            });
        }

        private void Handle(ClusterActorDiscoveryMessages.UnregisterCluster m)
        {
            _log.Info($"UnregisterCluster: Address={m.ClusterAddress}");

            var item = _nodeMap.FirstOrDefault(i => i.Value.ClusterAddress == m.ClusterAddress);
            if (item.Key != null)
                RemoveNode(item.Key);
        }

        private void RemoveNode(IActorRef discoveryActor)
        {
            NodeItem node;
            if (_nodeMap.TryGetValue(discoveryActor, out node) == false)
                return;

            _nodeMap.Remove(discoveryActor);

            foreach (var actorItem in node.ActorItems)
                NotifyActorDownToMonitor(actorItem.Actor, actorItem.Tag);
        }

        private void Handle(ClusterActorDiscoveryMessages.ClusterActorUp m)
        {
            _log.Debug($"ClusterActorUp: Address={m.Actor.Path} Tag={m.Tag}");

            NodeItem node;
            if (_nodeMap.TryGetValue(Sender, out node) == false)
            {
                _log.Error($"Cannot find node: Discovery={Sender.Path}");
                return;
            }
            node.ActorItems.Add(new ActorItem { Actor = m.Actor, Tag = m.Tag });

            NotifyActorUpToMonitor(m.Actor, m.Tag);
        }

        private void Handle(ClusterActorDiscoveryMessages.ClusterActorDown m)
        {
            _log.Debug($"ClusterActorDown: Address={m.Actor.Path}");

            NodeItem node;
            if (_nodeMap.TryGetValue(Sender, out node) == false)
            {
                _log.Error($"Cannot find node: Discovery={Sender.Path}");
                return;
            }

            // remove actor from node.ActorItems

            var index = node.ActorItems.FindIndex(a => a.Actor.Equals(m.Actor));
            if (index == -1)
            {
                _log.Error($"Cannot find actor: Discovery={Sender.Path} Actor={m.Actor.Path}");
                return;
            }

            var tag = node.ActorItems[index].Tag;
            node.ActorItems.RemoveAt(index);

            NotifyActorDownToMonitor(m.Actor, tag);
        }

        private void Handle(ClusterActorDiscoveryMessages.RegisterActor m)
        {
            _log.Debug($"RegisterActor: Actor={m.Actor.Path} Tag={m.Tag}");

            // add actor to _actorItems

            var index = _actorItems.FindIndex(a => a.Actor.Equals(m.Actor));
            if (index != -1)
            {
                _log.Error($"Already registered actor: Actor={m.Actor.Path} Tag={m.Tag}");
                return;
            }

            _actorItems.Add(new ActorItem { Actor = m.Actor, Tag = m.Tag });
            WatchActor(m.Actor, 0);

            // tell monitors & other discovery actors that local actor up

            NotifyActorUpToMonitor(m.Actor, m.Tag);
            foreach (var discoveryActor in _nodeMap.Keys)
                discoveryActor.Tell(new ClusterActorDiscoveryMessages.ClusterActorUp(m.Actor, m.Tag));
        }

        private void Handle(ClusterActorDiscoveryMessages.UnregisterActor m)
        {
            _log.Debug($"UnregisterActor: Actor={m.Actor.Path}");

            // remove actor from _actorItems

            var index = _actorItems.FindIndex(a => a.Actor.Equals(m.Actor));
            if (index == -1)
                return;

            var tag = _actorItems[index].Tag;
            _actorItems.RemoveAt(index);
            UnwatchActor(m.Actor, 0);

            // tell monitors & other discovery actors that local actor down

            NotifyActorDownToMonitor(m.Actor, tag);
            foreach (var discoveryActor in _nodeMap.Keys)
                discoveryActor.Tell(new ClusterActorDiscoveryMessages.ClusterActorDown(m.Actor));
        }

        private void Handle(ClusterActorDiscoveryMessages.MonitorActor m)
        {
            _log.Debug($"MonitorActor: Monitor={Sender.Path} Tag={m.Tag}");

            _monitorItems.Add(new MonitorItem { Actor = Sender, Tag = m.Tag });
            UnwatchActor(Sender, 1);

            // Send actor up message to just registered monitor

            foreach (var actor in _actorItems.Where(a => a.Tag == m.Tag))
                Sender.Tell(new ClusterActorDiscoveryMessages.ActorUp(actor.Actor, actor.Tag));

            foreach (var node in _nodeMap.Values)
            {
                foreach (var actor in node.ActorItems.Where(a => a.Tag == m.Tag))
                    Sender.Tell(new ClusterActorDiscoveryMessages.ActorUp(actor.Actor, actor.Tag));
            }
        }

        private void Handle(ClusterActorDiscoveryMessages.UnmonitorActor m)
        {
            _log.Debug($"UnmonitorActor: Monitor={Sender.Path} Tag={m.Tag}");

            var count = _monitorItems.RemoveAll(w => w.Actor.Equals(Sender) && w.Tag == m.Tag);
            for (var i = 0; i < count; i++)
                UnwatchActor(Sender, 1);
        }

        private void Handle(Terminated m)
        {
            _log.Debug($"Terminated: Actor={m.ActorRef.Path}");

            int[] counts;
            if (_actorWatchCountMap.TryGetValue(m.ActorRef, out counts) == false)
                return;

            if (counts[1] > 0)
            {
                _monitorItems.RemoveAll(w => w.Actor.Equals(Sender));
                counts[1] = 0;
            }
            if (counts[0] > 0)
            {
                var index = _actorItems.FindIndex(a => a.Actor.Equals(m.ActorRef));
                if (index != -1)
                {
                    var tag = _actorItems[index].Tag;
                    _actorItems.RemoveAt(index);

                    // tell monitors & other discovery actors that local actor down

                    NotifyActorDownToMonitor(m.ActorRef, tag);
                    foreach (var discoveryActor in _nodeMap.Keys)
                        discoveryActor.Tell(new ClusterActorDiscoveryMessages.ClusterActorDown(m.ActorRef));
                }
            }
        }

        private void NotifyActorUpToMonitor(IActorRef actor, string tag)
        {
            foreach (var monitor in _monitorItems.Where(w => w.Tag == tag))
                monitor.Actor.Tell(new ClusterActorDiscoveryMessages.ActorUp(actor, tag));
        }

        private void NotifyActorDownToMonitor(IActorRef actor, string tag)
        {
            foreach (var monitor in _monitorItems.Where(w => w.Tag == tag))
                monitor.Actor.Tell(new ClusterActorDiscoveryMessages.ActorDown(actor, tag));
        }

        private void WatchActor(IActorRef actor, int channel)
        {
            int[] counts;
            if (_actorWatchCountMap.TryGetValue(actor, out counts))
            {
                counts[channel] += 1;
                return;
            }

            counts = new int[2];
            counts[channel] += 1;
            _actorWatchCountMap.Add(actor, counts);
            Context.Watch(actor);
        }

        private void UnwatchActor(IActorRef actor, int channel)
        {
            int[] counts;
            if (_actorWatchCountMap.TryGetValue(actor, out counts) == false)
                return;

            counts[channel] -= 1;
            if (counts.Sum() > 0)
                return;

            _actorWatchCountMap.Remove(actor);
            Context.Unwatch(actor);
        }
    }
}
