# LLRP - Low Level Reverse Proxy

Microsoft Hackaton 2021 project

| load                | YARP      | HttpClient |         | LLRP       |          |
| ------------------- | --------- | ---------- | ------- | ---------- | -------- |
| Requests            | 7.510.371 | 13.593.730 | +81,00% | 16.231.362 | +116,12% |
| Bad responses       |         0 |          0 |         |          0 |          |
| Mean latency (us)   |     1.020 |        562 | -44,86% |        470 |  -53,87% |
| Requests/sec        | **250.548** | **453.170** | **+80,87%** | **541.115** | **+115,97%** |
| Requests/sec (max)  |   272.317 |    476.101 | +74,83% |    584.554 | +114,66% |


```
dotnet tool update Microsoft.Crank.Controller -g --version "0.2.0-*"

# These require corpnet access for aspnet-perf-lin
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario yarp       --json YARP.json
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario httpclient --json HttpClient.json
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario llrp       --json LLRP.json

crank compare YARP.json HttpClient.json LLRP.json
```