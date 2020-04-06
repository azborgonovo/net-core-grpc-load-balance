# Loadbalancing using sidecar proxy for gRPC dotnet client

## Overview

![Overview](./overview.PNG)

__NOTE: Run commands in root directory__

__NOTE: K8s files works with local docker images, change imagePullPolicy to allow remote registry__

## Build images
```
docker build -t grpc-dotnet-envoy-sidecar:latest -f .\Envoy.Sidecar\Dockerfile .\Envoy.Sidecar
docker build -t grpc-dotnet-client-sidecar:latest -f .\NetCoreGrpc.DotNet.RegularClient.ConsoleClientApp\Dockerfile .
docker build -t grpc-server:latest -f .\NetCoreGrpc.ServerApp\Dockerfile .
```

## Create resources in K8s
```
kubectl apply -f .\k8s\grpc-server.yaml
kubectl create -f .\k8s\grpc-dotnet-client-sidecar.yaml
```

## Verify connection
```
kubectl logs grpc-dotnet-client-sidecar grpc-dotnet-client-sidecar
```

## Tear down resources
```
kubectl delete -f .\k8s\grpc-dotnet-client-sidecar.yaml
kubectl delete -f .\k8s\grpc-server.yaml
```

## Verify DNS records
```
kubectl apply -f .\utils\dnsutils.yaml
kubectl exec -ti dnsutils -- nslookup -type=A grpc-server.default.svc.cluster.local
kubectl delete -f .\utils\dnsutils.yaml
```