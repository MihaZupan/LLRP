# LLRP - Low Level Reverse Proxy

Microsoft Hackaton 2021 project

| load                | YARP      | HttpClient |         | LLRP      |          |
| ------------------- | --------- | ---------- | ------- | --------- | -------- |
| CPU Usage (%)       |        29 |         48 | +65,52% |        67 | +131,03% |
| Cores usage (%)     |       809 |      1.336 | +65,14% |     1.890 | +133,62% |
| First Request (ms)  |       178 |        151 | -15,17% |        99 |  -44,38% |
| Requests            | 3.664.062 |  5.847.333 | +59,59% | 7.833.492 | +113,79% |
| Bad responses       |         0 |          0 |         |         0 |          |
| Mean latency (us)   |     1.045 |        654 | -37,39% |       487 |  -53,38% |
| Max latency (us)    |    40.402 |     43.787 |  +8,38% |    64.460 |  +59,55% |
| **Requests/sec**    | **244.405** | **390.393** | **+59,73%** | **522.619** | **+113,83%** |
| Requests/sec (max)  |   267.091 |    443.037 | +65,87% |   601.288 | +125,12% |

```
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario yarp       --json YARP.json
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario httpclient --json HttpClient.json
crank --config benchmarks.yml --profile aspnet-perf-lin --scenario llrp       --json LLRP.json

crank compare YARP.json HttpClient.json LLRP.json
```