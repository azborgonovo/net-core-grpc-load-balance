using System;
using Grpc.Core;
using System.Threading;
using NetCoreGrpc.LoadBalance.Proto;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Grpc.Net.Client.LoadBalancing;
using Grpc.Net.Client.LoadBalancing.Extensions;

namespace NetCoreGrpc.DotNet.LoadBalanceExternal.ConsoleClientApp
{
    public class Program
    {
        public static void Main()
        {
            var channelOptions = new GrpcChannelOptions()
            {
                LoggerFactory = GetConsoleLoggerFactory(),
                HttpClient = CreateGrpcHttpClient(acceptSelfSignedCertificate: true),
                ResolverPlugin = GetGrpcResolverPlugin(),
            };
            var channelTarget = Environment.GetEnvironmentVariable("SERVICE_TARGET");
            var channel = GrpcChannel.ForAddress(channelTarget, channelOptions);
            var client = new Greeter.GreeterClient(channel);
            var user = "Pawel";
            for (int i = 0; i < 10000; i++)
            {
                try
                {
                    var reply = client.SayHello(new HelloRequest { Name = user });
                    Console.WriteLine("Greeting: " + reply.Message);
                }
                catch (RpcException e)
                {
                    Console.WriteLine("Error invoking: " + e.Status);
                }
                Thread.Sleep(1000);
            }
            channel.ShutdownAsync().Wait();
            Console.WriteLine();
        }

        private static ILoggerFactory GetConsoleLoggerFactory()
        {
            var isLocalEnvironment = bool.TryParse(Environment.GetEnvironmentVariable("IS_LOCAL_ENV"), out bool x) ? x : false;
            if (isLocalEnvironment)
            {
                return NullLoggerFactory.Instance;
            }
            return LoggerFactory.Create(x =>
            {
                x.AddConsole();
                x.SetMinimumLevel(LogLevel.Trace);
            });
        }

        private static IGrpcResolverPlugin GetGrpcResolverPlugin()
        {
            var isLocalEnvironment = bool.TryParse(Environment.GetEnvironmentVariable("IS_LOCAL_ENV"), out bool x) ? x : false;
            if (isLocalEnvironment)
            {
                return new StaticResolverPlugin((uri) =>
                {
                    var hosts = new List<GrpcHostAddress>()
                    {
                        new GrpcHostAddress("127.0.0.1", 9000)
                        {
                            IsLoadBalancer = true,
                        }
                    };
                    var config = GrpcServiceConfigOrError.FromConfig(GrpcServiceConfig.Create("grpclb", "pick_first"));
                    return new GrpcNameResolutionResult(hosts, config, GrpcAttributes.Empty);
                });
            }
            else
            {
                return new DnsClientResolverPlugin(GetDnsClientResolverPluginOptions());
            }
        }

        private static DnsClientResolverPluginOptions GetDnsClientResolverPluginOptions()
        {
            const int defaultDnsPort = 53;
            DnsClientResolverPluginOptions options;
            var customDnsAddress = Environment.GetEnvironmentVariable("CUSTOM_DNS_ADDRESS");
            if (customDnsAddress != null)
            {
                options = new DnsClientResolverPluginOptions()
                {
                    NameServers = new IPEndPoint[] { new IPEndPoint(Dns.GetHostAddresses(customDnsAddress)[0], defaultDnsPort) },
                    EnableSrvGrpclb = true
                };
            }
            else
            {
                options = new DnsClientResolverPluginOptions()
                {
                    EnableSrvGrpclb = true
                };
            }
            return options;
        }

        private static HttpClient CreateGrpcHttpClient(bool acceptSelfSignedCertificate = false)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            if (acceptSelfSignedCertificate)
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;
                var httpClient = new HttpClient(handler);
                httpClient.Timeout = Timeout.InfiniteTimeSpan;
                return httpClient;
            }
            else
            {
                var httpClient = new HttpClient();
                httpClient.Timeout = Timeout.InfiniteTimeSpan;
                return httpClient;
            }
        }
    }
}
