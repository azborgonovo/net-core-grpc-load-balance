﻿using k8s;
using k8s.Exceptions;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetCoreGrpc.MyGrpcLoadBalancer.Services
{
    public sealed class KubernetesEndpointWatcher
    {
        private readonly Kubernetes _client;
        private IEnumerable<EndpointEntry> _endpointEntries;
        public KubernetesEndpointWatcher()
        {
            _endpointEntries = Enumerable.Empty<EndpointEntry>();
            try
            {
                var config = KubernetesClientConfiguration.InClusterConfig();
                _client = new Kubernetes(config);
                _client.WatchNamespacedEndpointsAsync("grpc-server", "default", onEvent: onEvent);
            }
            catch (KubeConfigException x) when (x.Message.StartsWith("unable to load in-cluster configuration"))
            {
                _endpointEntries = Array.Empty<EndpointEntry>();
            }
        }

        public IReadOnlyList<EndpointEntry> GetEndpointEntries()
        {
            return new List<EndpointEntry>(_endpointEntries).AsReadOnly();
        }

        private void onEvent(WatchEventType type, V1Endpoints endpoint)
        {
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                    ModifiedEndpoints(endpoint); break;
                case WatchEventType.Deleted:
                case WatchEventType.Error:
                    _endpointEntries = new List<EndpointEntry>(); break;
                default: throw new InvalidOperationException();
            }

        }

        private void ModifiedEndpoints(V1Endpoints endpoint)
        {
            var list = new List<EndpointEntry>();
            foreach (var subset in endpoint.Subsets)
            {
                var port = subset.Ports.First().Port;
                foreach (var address in subset.Addresses)
                {
                    list.Add(new EndpointEntry() { Ip = address.Ip, Port = port });
                }
            }
            _endpointEntries = list;
        }
    }

    public sealed class EndpointEntry
    {
        public string Ip { get; set; }
        public int Port { get; set; }
    }
}