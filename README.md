# LLRP - Low Level Reverse Proxy

Started as a Microsoft Hackathon 2021 project.

| load                   | YARP      | LLRP_HttpClient |         | LLRP_LLHTTP |         |
| ---------------------- | --------- | --------------- | ------- | ----------- | ------- |
| Requests               | 9.658.071 |      14.361.795 | +48,70% |  16.104.894 | +66,75% |
| Bad responses          |         0 |               0 |         |           0 |         |
| Mean latency (us)      |       792 |             532 | -32,86% |         474 | -40,17% |
| Max latency (us)       |    93.781 |          85.050 |  -9,31% |      69.737 | -25,64% |
| Requests/sec           |   **322.349** |         **479.094** | **+48,63%** |     **537.287** | **+66,68%** |

*numbers from `aspnet-citrine-lin`, using full PGO on .NET 7 preview 3.

## Running benchmarks

```
dotnet tool update Microsoft.Crank.Controller -g --version "0.2.0-*"

# These require corpnet access for aspnet-citrine-lin
crank --config https://raw.githubusercontent.com/MihaZupan/LLRP/main/benchmarks.yml --profile aspnet-citrine-lin --scenario yarp --json YARP.json --application.framework net7.0
crank --config https://raw.githubusercontent.com/MihaZupan/LLRP/main/benchmarks.yml --profile aspnet-citrine-lin --scenario llrp-httpclient --json LLRP_HttpClient.json
crank --config https://raw.githubusercontent.com/MihaZupan/LLRP/main/benchmarks.yml --profile aspnet-citrine-lin --scenario llrp-llhttp --json LLRP_LLHTTP.json

crank compare YARP.json LLRP_HttpClient.json LLRP_LLHTTP.json
```

To run the benchmarks with full PGO, append the following to the above commands:
```
--application.environmentVariables DOTNET_TieredPGO=1 --application.environmentVariables DOTNET_ReadyToRun=0 --application.environmentVariables DOTNET_TC_QuickJitForLoops=1
```

## Scenarios

The following scenarios are available as part of this repository's benchmark definition:
- `llrp-llhttp`: A low-level reverse proxy implementation based on Kestrel platform benchmarks, using [LLHTTP](https://github.com/dotnet/runtimelab/tree/feature/LLHTTP2) as the client for outgoing requests. See [LLRPApplication.cs](LLRPApplication.cs) for the implementation.
- `llrp-httpclient`: A low-level reverse proxy implementation based on Kestrel platform benchmarks, using `HttpClient` as the client for outgoing requests. See [HttpClientApplication.cs](HttpClientApplication.cs) for the implementation.
- `yarp`: A fully-featured [microsoft/reverse-proxy benchmark app](https://github.com/microsoft/reverse-proxy/tree/main/testassets/BenchmarkApp). Utilizes ASP.NET Core request parsing, routing, connection management and middleware. Uses [`HttpForwarder`](https://github.com/microsoft/reverse-proxy/blob/main/src/ReverseProxy/Forwarder/HttpForwarder.cs) for the proxying, relying on `HttpClient` for outgoing requests.
- `HttpClientProxy`: A reverse proxy using full ASP.NET Core and `HttpClient`. See the implementation [here](https://github.com/aspnet/Benchmarks/tree/main/src/BenchmarksApps/HttpClient/Proxy).
    - Differs from `yarp` in removing most of the functionality on top of raw `HttpClient.SendAsync` calls.
- `llrp-yarp`: A reverse proxy implementation based on Kestrel platform benchmarks that creates custom `HttpContext` and then calls into YARP's `HttpForwarder`. See [YarpApplication.cs](YarpApplication.cs) for the implementation.
    - Differs from `yarp` in removing most of ASP.NET Core functionality.
- `llrp-httpclientwithcontext`: Similar to `llrp-yarp` in that it creates a custom `HttpContext` object, but then relies on `HttpClient` for outgoing requests. See [HttpClientWithContextApplication.cs](HttpClientWithContextApplication.cs) for the implementation.
    - Differs from `llrp-yarp` in removing most of YARP's functionality on top of raw `HttpClient.SendAsync` calls.
    - Differs from `llrp-llhttp` in introducing the indirection through `HttpContext`, which is part of the cost of using full ASP.NET Core functionality.
- `haproxy`: An [HAProxy](http://www.haproxy.org/) benchmark used as a performance reference.
- `nginx`: An [NGINX](https://www.nginx.com/) benchmark used as a performance reference.

Except for `yarp`, `haproxy` and `nginx`, the above benchmarks are minimal implementations, offering minimal functionality, no extensibility or error handling, and may lack in behavioral correctness.

## Extra parameters

The benchmarks currently allow only cleartext HTTP/1.1 traffic in both directions.
Body sizes are not configurable.

`llrp-httpclient`, `llrp-yarp` and `llrp-httpclientwithcontext` scenarios support the following environment variables to configure how `HttpClient` instances are used:
- `shareClients`: Controls whether the `HttpClient` instances are shared between different incoming connections. Defaults to `false` - using a separate client instance for each connection.
- `shareClients.roundRobin`: Controls whether a given connection cycles between different `HttpClient` instances between requests. Defaults to `false` - using the same client instance for all requests on the connection.
- `shareClients.count`: Controls how many `HttpClient` instances are allocated and shared between all connections. Defaults to `1`, but has no effect unless `shareClients` is enabled.

Example - running two benchmarks comparing the performance of `llrp-httpclient` when using 1 vs 2 shared `HttpClient` instances:
```
crank --config https://raw.githubusercontent.com/MihaZupan/LLRP/main/benchmarks.yml --profile aspnet-citrine-lin --scenario llrp-httpclient --application.environmentVariables shareClients=true --application.environmentVariables shareClients.count=1 --json LLRP_HttpClient_1.json
crank --config https://raw.githubusercontent.com/MihaZupan/LLRP/main/benchmarks.yml --profile aspnet-citrine-lin --scenario llrp-httpclient --application.environmentVariables shareClients=true --application.environmentVariables shareClients.count=2 --json LLRP_HttpClient_2.json

crank compare LLRP_HttpClient_1.json LLRP_HttpClient_2.json
```