using System.Collections;
using BenchmarkDotNet.Running;

namespace LinqDeeperDive.SelectSumImpl
{
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
}

