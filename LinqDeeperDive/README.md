# Linq Deep Dive
This is the follow along code for the [Dive into LINQ with Stephen Toub](https://www.youtube.com/watch?v=W4-NVVNwCWs) on how linq optimise queries.

## Benchmarks 

| Method               | Mean     | Error     | StdDev    | Gen0   | Allocated |
|--------------------- |---------:|----------:|----------:|-------:|----------:|
| SumCompiler          | 7.413 us | 0.9054 us | 0.0496 us | 0.0076 |     104 B |
| OptimizedSumCompiler | 4.446 us | 0.2771 us | 0.0152 us |      - |      72 B |
| SumManual            | 7.099 us | 0.1981 us | 0.0109 us |      - |      88 B |
| OptimizedSumManual   | 3.864 us | 0.0894 us | 0.0049 us |      - |      48 B |
| SumLinq              | 4.449 us | 2.1760 us | 0.1193 us |      - |      32 B |
