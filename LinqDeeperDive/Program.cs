using System.Collections;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LinqDeeperDive;


[MemoryDiagnoser]
[ShortRunJob]
public class Tests
{
    private IEnumerable<int> source = Enumerable.Range(0, 1000).ToArray();

    [Benchmark]
    public int SumCompiler()
    {
        int sum = 0;
        foreach (int i in SelectCompiler(source, i => i + 2))
        {
            sum += i;
        }

        return sum;
    }


    [Benchmark]
    public int SumManual()
    {
        return -1;
    }

    [Benchmark]
    public int SumLinq()
    {
        return -1;
    }
   
    public static IEnumerable<TResult> SelectCompiler<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return Impl(source, selector);

        static IEnumerable<TResult> Impl(IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            foreach (var item in source)
            {
                yield return selector(item);
            }
        }
    }
}

