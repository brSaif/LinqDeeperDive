# Linq Deep Dive
This is the follow along code for the [Dive into LINQ with Stephen Toub](https://www.youtube.com/watch?v=W4-NVVNwCWs) on how linq optimise queries.

## Benchmarks 

| Method               | Job               | Runtime  | Mean     | Error     | StdDev   | Allocated |
|--------------------- |------------------ |--------- |---------:|----------:|---------:|----------:|
| SumCompiler          | ShortRun-.NET 7.0 | .NET 7.0 | 83.49 us | 13.406 us | 0.735 us |     104 B |
| OptimizedSumCompiler | ShortRun-.NET 7.0 | .NET 7.0 | 44.88 us |  5.347 us | 0.293 us |      72 B |
| SumManual            | ShortRun-.NET 7.0 | .NET 7.0 | 70.80 us |  4.299 us | 0.236 us |      88 B |
| OptimizedSumManual   | ShortRun-.NET 7.0 | .NET 7.0 | 41.00 us |  2.304 us | 0.126 us |      48 B |
| SumLinq              | ShortRun-.NET 7.0 | .NET 7.0 | 43.33 us |  0.733 us | 0.040 us |      32 B |
| SumCompiler          | ShortRun-.NET 8.0 | .NET 8.0 | 29.45 us |  5.127 us | 0.281 us |     104 B |
| OptimizedSumCompiler | ShortRun-.NET 8.0 | .NET 8.0 | 12.74 us |  1.169 us | 0.064 us |      72 B |
| SumManual            | ShortRun-.NET 8.0 | .NET 8.0 | 30.22 us |  5.777 us | 0.317 us |      88 B |
| OptimizedSumManual   | ShortRun-.NET 8.0 | .NET 8.0 | 10.85 us |  5.466 us | 0.300 us |      48 B |
| SumLinq              | ShortRun-.NET 8.0 | .NET 8.0 | 10.85 us |  4.010 us | 0.220 us |      32 B |
