using System.Collections;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

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

sealed class SelectManualEnumerable<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>
{
    private IEnumerable<TSource> _source;
    private Func<TSource, TResult> _selector;

    private int _threadId = Environment.CurrentManagedThreadId;
    private TResult _current = default!;
    private IEnumerator<TSource>? _enumerator;
    private int _state = 0;
    
    public SelectManualEnumerable(IEnumerable<TSource> source, Func<TSource, TResult> selector)
    {
        _source = source;
        _selector = selector;
    }

    public IEnumerator<TResult> GetEnumerator()
    {
        if (_threadId == Environment.CurrentManagedThreadId && _state == 0)
        {
            _state = 1;
            return this;
        }

        return new SelectManualEnumerable<TSource, TResult>(_source, _selector) { _state = 1 };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public bool MoveNext()
    {
        switch (_state)
        {
            case 1 :
                _enumerator = _source.GetEnumerator();
                _state = 2;
                goto case 2;
            case 2 :
                Debug.Assert(_enumerator is not null);

                try
                {
                    if (_enumerator.MoveNext())
                    {
                        _current = _selector(_enumerator.Current);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Dispose();
                    throw;
                }
                break;
        }
        
        Dispose();
        return false;
    }
    
    public void Dispose()
    {
        _state = -1;
        _enumerator?.Dispose();
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }

    public TResult Current => _current;

    object IEnumerator.Current => Current;

}

sealed class SelectManualArray<TSource, TResult> : IEnumerable<TResult>, IEnumerator<TResult>
{
    private TSource[] _source;
    private Func<TSource, TResult> _selector;

    private int _threadId = Environment.CurrentManagedThreadId;
    private TResult _current = default!;
    private int _state = 0;
    
    public SelectManualArray(TSource[] source, Func<TSource, TResult> selector)
    {
        _source = source;
        _selector = selector;
    }

    public IEnumerator<TResult> GetEnumerator()
    {
        if (_threadId == Environment.CurrentManagedThreadId && _state == 0)
        {
            _state = 1;
            return this;
        }

        return new SelectManualArray<TSource, TResult>(_source, _selector) { _state = 1 };
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    public bool MoveNext()
    {
        int i = _state - 1;
        TSource[] source = _source;
        
        if ((uint)i < (uint)source.Length)
        {
            _current = _selector(_source[i]);
            _state++;
            return true;
        }
        
        Dispose();
        return false;
    }
    
    public void Dispose()
    {
        _state = -1;
    }

    public void Reset()
    {
        throw new NotSupportedException();
    }

    public TResult Current => _current;

    object IEnumerator.Current => Current;

}

