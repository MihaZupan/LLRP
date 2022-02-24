using LLRP;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Configuration;
using PlatformBenchmarks;
using System.Linq;

Console.WriteLine($"Args: {string.Join(' ', args)}");

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddEnvironmentVariables(prefix: "ASPNETCORE_")
    .AddCommandLine(args)
    .Build();

var hostBuilder = new WebHostBuilder()
    .UseBenchmarksConfiguration(config)
    .UseKestrel((context, options) =>
    {
        var endPoint = context.CreateIPEndPoint();
        DownstreamAddress.DownstreamAddresses = config.GetDownstreamAddresses();

        config.ConfigureSharedHttpClients();

        if (config.ShouldUseHttpClient())
        {
            options.Listen(endPoint, builder =>
            {
                builder.UseHttpApplication<HttpClientApplication>();
            });
        }
        else if (config.ShouldUseHttpClientWithAspNetContext())
        {
            options.Listen(endPoint, builder =>
            {
                builder.UseHttpApplication<HttpClientWithContextApplication>();
            });
        }
        else if (config.ShouldUseYarp())
        {
            options.Listen(endPoint, builder =>
            {
                builder.UseHttpApplication<YarpApplication>();
            });
        }
        else
        {
            options.Listen(endPoint, builder =>
            {
                builder.UseHttpApplication<LLRPApplication>();
            });
        }
    })
    .UseSockets(options =>
    {
        options.WaitForDataBeforeAllocatingBuffer = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            options.UnsafePreferInlineScheduling = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS") == "1";
        }
    })
    .UseStartup<Startup>();

var host = hostBuilder.Build();

await host.RunAsync();

namespace LLRP
{
    public interface IHttpConnection : IHttpHeadersHandler, IHttpRequestLineHandler
    {
        PipeReader Reader { get; set; }
        PipeWriter Writer { get; set; }

        Task ExecuteAsync();
    }
}

namespace PlatformBenchmarks
{
    public static class HttpApplicationConnectionBuilderExtensions
    {
        public static IConnectionBuilder UseHttpApplication<TConnection>(this IConnectionBuilder builder) where TConnection : IHttpConnection, new()
        {
            Console.WriteLine($"Using {typeof(TConnection).Name}");
            return builder.Use(next => new HttpApplication<TConnection>().ExecuteAsync);
        }
    }

    public class HttpApplication<TConnection> where TConnection : IHttpConnection, new()
    {
        public Task ExecuteAsync(ConnectionContext connection)
        {
            var httpConnection = new TConnection
            {
                Reader = connection.Transport.Input,
                Writer = connection.Transport.Output
            };
            return httpConnection.ExecuteAsync();
        }
    }

    public static class BenchmarkConfigurationHelpers
    {
        public static IWebHostBuilder UseBenchmarksConfiguration(this IWebHostBuilder builder, IConfiguration configuration)
        {
            builder.UseConfiguration(configuration);

            // Handle the transport type
            var webHost = builder.GetSetting("KestrelTransport");

            Console.WriteLine($"Transport: {webHost}");

            builder.UseSockets(options =>
            {
                if (int.TryParse(builder.GetSetting("threadCount"), out int threadCount))
                {
                    options.IOQueueCount = threadCount;
                }

#if NETCOREAPP5_0 || NET5_0 || NET6_0
                options.WaitForDataBeforeAllocatingBuffer = false;

                Console.WriteLine($"Options: WaitForData={options.WaitForDataBeforeAllocatingBuffer}, IOQueue={options.IOQueueCount}");
#endif
            });

            return builder;
        }

        public static BindingAddress CreateBindingAddress(this IConfiguration config)
        {
            var url = config["server.urls"] ?? config["urls"];

            if (string.IsNullOrEmpty(url))
            {
                return BindingAddress.Parse("http://localhost:5000");
            }

            return BindingAddress.Parse(url);
        }

        public static IPEndPoint CreateIPEndPoint(this WebHostBuilderContext context)
        {
            var address = context.Configuration.CreateBindingAddress();

            IPAddress? ip;

            if (string.Equals(address.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                ip = IPAddress.Loopback;
            }
            else if (!IPAddress.TryParse(address.Host, out ip))
            {
                ip = IPAddress.IPv6Any;
            }

            return new IPEndPoint(ip, address.Port);
        }

        public static DownstreamAddress[] GetDownstreamAddresses(this IConfiguration config)
        {
            //string list = config["downstream"] ?? "http://httpbin.org/anything/A";
            string list = config["downstream"] ?? "http://localhost:8081";
            return list.Split(';').Select(u => new DownstreamAddress(new Uri(u, UriKind.Absolute))).ToArray();
        }

        public static bool ShouldUseHttpClient(this IConfiguration config)
        {
            return config.GetFlag("httpclient");
        }

        public static bool ShouldUseHttpClientWithAspNetContext(this IConfiguration config)
        {
            return config.GetFlag("httpclient-httpcontext");
        }

        public static bool ShouldUseYarp(this IConfiguration config)
        {
            return config.GetFlag("yarp");
        }

        public static void ConfigureSharedHttpClients(this IConfiguration config)
        {
            if (HttpClientConfiguration.ShareClients = config.GetFlag("shareClients"))
            {
                HttpClientConfiguration.RoundRobin = config.GetFlag("shareClients.roundRobin");

                int count = config["shareClients.count"] is string clientCount
                    ? int.Parse(clientCount)
                    : 1;

                HttpClientConfiguration.SharedClients = Enumerable.Range(0, count)
                    .Select(_ => HttpClientConfiguration.CreateClient())
                    .ToArray();
            }
        }

        private static bool GetFlag(this IConfiguration config, string flag)
        {
            string? value = config[flag]?.ToLowerInvariant();
            return value == "true" || value == "1";
        }
    }

    public sealed class Startup
    {
        public void Configure() { }
    }
}
