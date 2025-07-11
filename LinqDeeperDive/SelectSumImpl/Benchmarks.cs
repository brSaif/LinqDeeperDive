using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace LinqDeeperDive.SelectSumImpl;

[MemoryDiagnoser]
[ShortRunJob(RuntimeMoniker.Net70)]
[ShortRunJob(RuntimeMoniker.Net80)]
// [ShortRunJob(RuntimeMoniker.Net90)]
public class Benchmarks
{
    private IEnumerable<int> source = Enumerable.Range(0, 10000).ToArray();

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
    public int OptimizedSumCompiler()
    {
        int sum = 0;
        foreach (int i in OptimizedSelectCompiler(source, i => i + 2))
        {
            sum += i;
        }

        return sum;
    }


    [Benchmark]
    public int SumManual()
    {
        int sum = 0;
        
        foreach (int i in SelectManual(source, i => i + 2))
        {
            sum += i;
        }

        return sum;
    }
    
    [Benchmark]
    public int OptimizedSumManual()
    {
        int sum = 0;
        
        foreach (int i in OptimizedSelectManual(source, i => i + 2))
        {
            sum += i;
        }

        return sum;
    }

    [Benchmark]
    public int SumLinq()
    {
        return source.Sum(i => i + 2);
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
    
    public static IEnumerable<TResult> OptimizedSelectCompiler<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source is TSource[] array)
        {
            return ArrayImpl(array, selector);
        }
        
        return EnumerableImpl(source, selector);

        static IEnumerable<TResult> EnumerableImpl(IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            foreach (var item in source)
            {
                yield return selector(item);
            }
        }
        
        static IEnumerable<TResult> ArrayImpl(TSource[] source, Func<TSource, TResult> selector)
        {
            foreach (var item in source)
            {
                yield return selector(item);
            }
            
            // the above code equivalent to in the lower level c#:
            // for (int i = 0; i < source.Length; i++)
            // {
            //     yield return selector(source[i]);
            // }
        }
    }

    public static IEnumerable<TResult> SelectManual<TSource, TResult>(IEnumerable<TSource> source,
        Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return new SelectManualEnumerable<TSource, TResult>(source, selector);
    }
    
    public static IEnumerable<TResult> OptimizedSelectManual<TSource, TResult>(IEnumerable<TSource> source,
        Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        if (source is TSource[] array)
        {
            return new SelectManualArray<TSource, TResult>(array, selector);
        }
        
        return new SelectManualEnumerable<TSource, TResult>(source, selector);
    }
}