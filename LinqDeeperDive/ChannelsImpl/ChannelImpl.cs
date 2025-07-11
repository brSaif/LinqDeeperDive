using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace LinqDeeperDive.ChannelsImpl
{
    public class ChannelImpl
    {
        public class DefaultChannel
        {
            /// <summary>
            /// Represents a simple example of using an unbounded channel to write and read integers asynchronously.
            /// </summary>
            public static async Task Run()
            {
                var unboundedChannel = Channel.CreateUnbounded<int>();

                _ = Task.Run(async () => 
                {
    
                    for (int i = 0; i < 10; i++)
                    {
                        await unboundedChannel.Writer.WriteAsync(i);
    
                        await Task.Delay(1000);
                    }
    
                    unboundedChannel.Writer.Complete();
                });

                while (true)
                {
                    Console.WriteLine(await unboundedChannel.Reader.ReadAsync());
                }
            }
        }
    
        /// <summary>
        /// Represents a simplistic implementation of a channel that allows basic writing and reading elements asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <remarks>Contains a bug</remarks>
        public class SimpleChannelImpl<T>
        {
            private readonly Queue<T> _items = []; 
            private readonly Queue<TaskCompletionSource<T>> _readers = [];
            private bool _isCompleted;
            private object syncObj => _items;


            /// <summary>
            /// writes an element to the channel asynchronously.
            /// </summary>
            /// <param name="item">The element to write.</param>
            /// <returns>Value task of type <see cref="ValueTask{T}"/></returns>
            /// <remarks>Value task is a struct used for when async methods are likely to complete
            /// synchronously and hence no need for extra allocation, in which case its <see cref="T"/> field will be filled
            /// but it <see cref="Task{T}"/> field will be null</remarks>
            public ValueTask<T> WriteAsync(T item)
            {
                lock (syncObj)
                {
                    if (_readers.TryDequeue(out var tcs))
                    {
                        // If there is a reader waiting, we complete it with the item
                        tcs.SetResult(item);
                    }
                    else
                    {
                        _items.Enqueue(item);
                    }
                }

                return default;
            }

            public ValueTask<T> ReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.TryDequeue(out var item))
                    {
                        return new ValueTask<T>(item);
                    }

                    if (_isCompleted)
                    {
                        return ValueTask.FromException<T>(new InvalidOperationException("Channel is completed"));
                    }
                
                    // The TaskCompletionSource creates a task that could be completed later,
                    // as it still hold on the producer side 
                    var tcs = new TaskCompletionSource<T>();
                    _readers.Enqueue(tcs);
                    return new ValueTask<T>(tcs.Task); 
                }
            }

            public void Complete()
            {
                lock (syncObj)
                {
                    _isCompleted = true;

                    while (_readers.TryDequeue(out var tcs))
                    {
                        tcs.SetException(new InvalidOperationException("Channel is completed"));
                    }
                }
            }
        
            public static async Task Run()
            {
                var c = new SimpleChannelImpl<int>();

                _ = Task.Run(async () => 
                {
    
                    for (int i = 0; i < 10; i++)
                    {
                        await c.WriteAsync(i);
    
                        await Task.Delay(1000);
                    }
    
                    c.Complete();
                });

                while (true)
                {
                    Console.WriteLine(await c.ReadAsync());
                }
            }
        }
    
        /// <summary>
        /// This implementation introduces a livelock bug where threads remain active (not blocked) but do not make progress
        /// as they keep interacting with each other.
        /// </summary>
        /// <typeparam name="T">the type of the item</typeparam>
        /// <remarks>
        /// The main reason for this livelock is the handling of a synchronous operation in an asynchronous context,
        ///
        /// Livelock is different from a spinlock as the latter is a low-level synchronization primitive where a thread
        /// repeatedly polling waiting (or "spins") for something to complete.
        ///
        /// Livelock differs from deadlock in that in a deadlock, when debugging no progress is made and threads are blocked,
        /// but in the first case, the debugger can still move around and see that threads are active,
        /// </remarks>
        /// <href>https://www.intel.com/content/www/us/en/developer/articles/technical/spin-locks-considered-harmful.html</href>
        public class BuggyChannelImpl<T>
        {
            private readonly Queue<T> _items = []; 
            private readonly Queue<TaskCompletionSource<T>> _readers = [];
            private TaskCompletionSource<bool>? _waitingReaders;
            private bool _isCompleted;
            private object syncObj => _items;


            /// <summary>
            /// writes an element to the channel asynchronously.
            /// </summary>
            /// <param name="item">The element to write.</param>
            /// <returns>Value task of type <see cref="ValueTask{T}"/></returns>
            /// <remarks>Value task is a struct used for when async methods are likely to complete
            /// synchronously and hence no need for extra allocation, in which case its <see cref="T"/> field will be filled
            /// but it <see cref="Task{T}"/> field will be null</remarks>
            public ValueTask<T> WriteAsync(T item)
            {
                lock (syncObj)
                {
                    if (_readers.TryDequeue(out var tcs))
                    {
                        // If there is a reader waiting, we complete it with the item
                        tcs.SetResult(item);
                    }
                    else
                    {
                        _items.Enqueue(item);

                        if (_waitingReaders is {})
                        {
                            _waitingReaders.SetResult(true);
                            _waitingReaders = null;
                        }
                    }
                }

                return default;
            }

            public ValueTask<T> ReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.TryDequeue(out var item))
                    {
                        return new ValueTask<T>(item);
                    }

                    if (_isCompleted)
                    {
                        return ValueTask.FromException<T>(new InvalidOperationException("Channel is completed"));
                    }
                
                    // The TaskCompletionSource creates a task that could be completed later,
                    // as it still hold on the producer side 
                    var tcs = new TaskCompletionSource<T>();
                    _readers.Enqueue(tcs);
                    return new ValueTask<T>(tcs.Task); 
                }
            }

            public void Complete()
            {
                lock (syncObj)
                {
                    _isCompleted = true;

                    while (_readers.TryDequeue(out var tcs))
                    {
                        tcs.SetException(new InvalidOperationException("Channel is completed"));
                    }
                
                    if (_waitingReaders is {})
                    {
                        _waitingReaders.SetResult(false);
                        _waitingReaders = null;
                    }
                }
            }

            /// <summary>
            /// Waits for an item to be available in the channel and returns it asynchronously.
            /// </summary>
            /// <returns>True if data is available, false otherwise</returns>
            public ValueTask<bool> WaitToReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.Count > 0)
                    {
                        return new ValueTask<bool>(true);
                    }

                    if (_isCompleted)
                    {
                        return new ValueTask<bool>(false);
                    }
            
                    _waitingReaders ??= new TaskCompletionSource<bool>();
                    return new ValueTask<bool>(_waitingReaders.Task);
                }

            }
        
            /// <summary>
            /// Attempts to read an element from the channel if one is available.
            /// </summary>
            /// <param name="item">The output parameter to store the item if successfully read.</param>
            /// <returns>A boolean value indicating whether an element was successfully read. Returns true if an item was available and read successfully; otherwise, false.</returns>
            private bool TryRead([MaybeNullWhen(false)] out T item)
            {
                lock (syncObj)
                {
                    return _items.TryDequeue(out item);
                }
            }
        
            /// <summary>
            /// This method demonstrates a livelock where the <see cref="BuggyChannelImpl{T}"/> WriteAsync sets the result of the value task
            /// then before it nulls <see cref="_waitingReaders"/> invokes the <see cref="WaitToReadAsync"/> method, which skips the the queue
            /// and the <see cref="_isCompleted"/> checks as neither will be true then finds the <see cref="_waitingReaders"/> not null,
            /// then calls the <see cref="TryRead"/> method on it which fails to dequeue the item.
            /// </summary>
            public static async Task Run()
            {
                var c = new BuggyChannelImpl<int>();

                _ = Task.Run(async () => 
                {
    
                    for (int i = 0; i < 10; i++)
                    {
                        await c.WriteAsync(i);
    
                        await Task.Delay(1000);
                    }
    
                    c.Complete();
                });

                while (await c.WaitToReadAsync())
                {
                    if (c.TryRead(out var item))
                    {
                        Console.WriteLine(item);
                    }
                }
            }

        }
    
        /// <summary>
        /// This makes the code work but does not fix the livelock issue. as the continuation of tasks still
        /// happening synchronously and therefore it could manifest. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class BuggyChannelSimpleFixImpl<T>
        {
            private readonly Queue<T> _items = []; 
            private readonly Queue<TaskCompletionSource<T>> _readers = [];
            private TaskCompletionSource<bool>? _waitingReaders;
            private bool _isCompleted;
            private object syncObj => _items;


            /// <summary>
            /// writes an element to the channel asynchronously.
            /// </summary>
            /// <param name="item">The element to write.</param>
            /// <returns>Value task of type <see cref="ValueTask{T}"/></returns>
            /// <remarks>Value task is a struct used for when async methods are likely to complete
            /// synchronously and hence no need for extra allocation, in which case its <see cref="T"/> field will be filled
            /// but it <see cref="Task{T}"/> field will be null</remarks>
            public ValueTask<T> WriteAsync(T item)
            {
                lock (syncObj)
                {
                    if (_readers.TryDequeue(out var tcs))
                    {
                        // If there is a reader waiting, we complete it with the item
                        tcs.SetResult(item);
                    }
                    else
                    {
                        _items.Enqueue(item);

                        var waitingReaders = _waitingReaders;
                        _waitingReaders = null;
                        waitingReaders?.SetResult(true);
                    }
                }

                return default;
            }

            public ValueTask<T> ReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.TryDequeue(out var item))
                    {
                        return new ValueTask<T>(item);
                    }

                    if (_isCompleted)
                    {
                        return ValueTask.FromException<T>(new InvalidOperationException("Channel is completed"));
                    }
                
                    // The TaskCompletionSource creates a task that could be completed later,
                    // as it still hold on the producer side 
                    var tcs = new TaskCompletionSource<T>();
                    _readers.Enqueue(tcs);
                    return new ValueTask<T>(tcs.Task); 
                }
            }

            public void Complete()
            {
                lock (syncObj)
                {
                    _isCompleted = true;

                    while (_readers.TryDequeue(out var tcs))
                    {
                        tcs.SetException(new InvalidOperationException("Channel is completed"));
                    }

                    var waitingReaders = _waitingReaders;
                    waitingReaders?.SetResult(false);
                    _waitingReaders = null;
                }
            }

            /// <summary>
            /// Waits for an item to be available in the channel and returns it asynchronously.
            /// </summary>
            /// <returns>True if data is available, false otherwise</returns>
            public ValueTask<bool> WaitToReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.Count > 0)
                    {
                        return new ValueTask<bool>(true);
                    }

                    if (_isCompleted)
                    {
                        return new ValueTask<bool>(false);
                    }
            
                    _waitingReaders ??= new TaskCompletionSource<bool>();
                    return new ValueTask<bool>(_waitingReaders.Task);
                }

            }
        
            /// <summary>
            /// Attempts to read an element from the channel if one is available.
            /// </summary>
            /// <param name="item">The output parameter to store the item if successfully read.</param>
            /// <returns>A boolean value indicating whether an element was successfully read. Returns true if an item was available and read successfully; otherwise, false.</returns>
            private bool TryRead([MaybeNullWhen(false)] out T item)
            {
                lock (syncObj)
                {
                    return _items.TryDequeue(out item);
                }
            }
        
            public static async Task Run()
            {
                var c = new BuggyChannelSimpleFixImpl<int>();

                _ = Task.Run(async () => 
                {
    
                    for (int i = 0; i < 10; i++)
                    {
                        await c.WriteAsync(i);
    
                        await Task.Delay(1000);
                    }
    
                    c.Complete();
                });

                while (await c.WaitToReadAsync())
                {
                    if (c.TryRead(out var item))
                    {
                        Console.WriteLine(item);
                    }
                }
            }

        }
    
        /// <summary>
        /// This implementation forces the continuation of tasks to happen asynchronously
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class FixedBuggyChannelImpl<T>
        {
            private readonly Queue<T> _items = []; 
            private readonly Queue<TaskCompletionSource<T>> _readers = [];
            private TaskCompletionSource<bool>? _waitingReaders;
            private bool _isCompleted;
            private object syncObj => _items;


            /// <summary>
            /// writes an element to the channel asynchronously.
            /// </summary>
            /// <param name="item">The element to write.</param>
            /// <returns>Value task of type <see cref="ValueTask{T}"/></returns>
            /// <remarks>Value task is a struct used for when async methods are likely to complete
            /// synchronously and hence no need for extra allocation, in which case its <see cref="T"/> field will be filled
            /// but it <see cref="Task{T}"/> field will be null</remarks>
            public ValueTask<T> WriteAsync(T item)
            {
                lock (syncObj)
                {
                    if (_readers.TryDequeue(out var tcs))
                    {
                        // If there is a reader waiting, we complete it with the item
                        tcs.SetResult(item);
                    }
                    else
                    {
                        _items.Enqueue(item);

                        var waitingReaders = _waitingReaders;
                        _waitingReaders = null;
                        waitingReaders?.SetResult(true);
                    }
                }

                return default;
            }

            public ValueTask<T> ReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.TryDequeue(out var item))
                    {
                        return new ValueTask<T>(item);
                    }

                    if (_isCompleted)
                    {
                        return ValueTask.FromException<T>(new InvalidOperationException("Channel is completed"));
                    }
                
                    // The TaskCompletionSource creates a task that could be completed later,
                    // as it still hold on the producer side 
                    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _readers.Enqueue(tcs);
                    return new ValueTask<T>(tcs.Task); 
                }
            }

            public void Complete()
            {
                lock (syncObj)
                {
                    _isCompleted = true;

                    while (_readers.TryDequeue(out var tcs))
                    {
                        tcs.SetException(new InvalidOperationException("Channel is completed"));
                    }

                    var waitingReaders = _waitingReaders;
                    waitingReaders?.SetResult(false);
                    _waitingReaders = null;
                }
            }

            /// <summary>
            /// Waits for an item to be available in the channel and returns it asynchronously.
            /// </summary>
            /// <returns>True if data is available, false otherwise</returns>
            public ValueTask<bool> WaitToReadAsync()
            {
                lock (syncObj)
                {
                    if (_items.Count > 0)
                    {
                        return new ValueTask<bool>(true);
                    }

                    if (_isCompleted)
                    {
                        return new ValueTask<bool>(false);
                    }
            
                    _waitingReaders ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return new ValueTask<bool>(_waitingReaders.Task);
                }

            }
        
            /// <summary>
            /// Attempts to read an element from the channel if one is available.
            /// </summary>
            /// <param name="item">The output parameter to store the item if successfully read.</param>
            /// <returns>A boolean value indicating whether an element was successfully read. Returns true if an item was available and read successfully; otherwise, false.</returns>
            private bool TryRead([MaybeNullWhen(false)] out T item)
            {
                lock (syncObj)
                {
                    return _items.TryDequeue(out item);
                }
            }
        
            public static async Task Run()
            {
                var c = new FixedBuggyChannelImpl<int>();

                _ = Task.Run(async () => 
                {
    
                    for (int i = 0; i < 10; i++)
                    {
                        await c.WriteAsync(i);
    
                        await Task.Delay(1000);
                    }
    
                    c.Complete();
                });

                while (await c.WaitToReadAsync())
                {
                    if (c.TryRead(out var item))
                    {
                        Console.WriteLine(item);
                    }
                }
            }

        }
    
        /// <summary>
        /// This implementation forces the continuation of tasks to happen asynchronously 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class FinalChannelImpl<T>
        {
            private readonly ConcurrentQueue<T> _items = []; 
            private readonly Queue<TaskCompletionSource<T>> _readers = [];
            private TaskCompletionSource<bool>? _waitingReaders;
            private bool _isCompleted;
            private object syncObj => _items;


            /// <summary>
            /// writes an element to the channel asynchronously.
            /// </summary>
            /// <param name="item">The element to write.</param>
            /// <returns>Value task of type <see cref="ValueTask{T}"/></returns>
            /// <remarks>Value task is a struct used for when async methods are likely to complete
            /// synchronously and hence no need for extra allocation, in which case its <see cref="T"/> field will be filled
            /// but it <see cref="Task{T}"/> field will be null</remarks>
            public ValueTask<T> WriteAsync(T item)
            {
                lock (syncObj)
                {
                    if (_readers.TryDequeue(out var tcs))
                    {
                        // If there is a reader waiting, we complete it with the item
                        tcs.SetResult(item);
                    }
                    else
                    {
                        _items.Enqueue(item);

                        var waitingReaders = _waitingReaders;
                        _waitingReaders = null;
                        waitingReaders?.SetResult(true);
                    }
                }

                return default;
            }

            /// <summary>
            /// This method uses a double-check locking pattern, once outside the lock and once inside.
            /// </summary>
            /// <returns></returns>
            public ValueTask<T> ReadAsync()
            {
                if (_items.TryDequeue(out var item))
                {
                    return new ValueTask<T>(item);
                }
            
                lock (syncObj)
                {
                    if (_items.TryDequeue(out  item))
                    {
                        return new ValueTask<T>(item);
                    }

                    if (_isCompleted)
                    {
                        return ValueTask.FromException<T>(new InvalidOperationException("Channel is completed"));
                    }
                
                    // The TaskCompletionSource creates a task that could be completed later,
                    // as it still hold on the producer side 
                    var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _readers.Enqueue(tcs);
                    return new ValueTask<T>(tcs.Task); 
                }
            }

            public void Complete()
            {
                lock (syncObj)
                {
                    _isCompleted = true;

                    while (_readers.TryDequeue(out var tcs))
                    {
                        tcs.SetException(new InvalidOperationException("Channel is completed"));
                    }

                    var waitingReaders = _waitingReaders;
                    waitingReaders?.SetResult(false);
                    _waitingReaders = null;
                }
            }

            /// <summary>
            /// Waits for an item to be available in the channel and returns it asynchronously.
            /// </summary>
            /// <returns>True if data is available, false otherwise</returns>
            public ValueTask<bool> WaitToReadAsync()
            {
                lock (syncObj)
                {
                
                    if (!_items.IsEmpty)
                    {
                        return new ValueTask<bool>(true);
                    }

                    if (_isCompleted)
                    {
                        return new ValueTask<bool>(false);
                    }
                
                    _waitingReaders ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    return new ValueTask<bool>(_waitingReaders.Task);
                }

            }
        
            /// <summary>
            /// Attempts to read an element from the channel if one is available.
            /// </summary>
            /// <param name="item">The output parameter to store the item if successfully read.</param>
            /// <returns>A boolean value indicating whether an element was successfully read. Returns true if an item was available and read successfully; otherwise, false.</returns>
            public bool TryRead([MaybeNullWhen(false)] out T item)
            {
                return _items.TryDequeue(out item);
            }
        
            public static async Task Run()
            {
                var c = new FinalChannelImpl<int>();

                _ = Task.Run(async () => 
                {
                    for (int i = 0; ; i++)
                    {
                        await c.WriteAsync(i);
                    }
                });
            
                long consumed = 0;
            
                _ = Task.Run( () => 
                {

                    while (true)
                    {
                        long start = consumed;
                        Thread.Sleep(1000);
                        long end = consumed;
                    
                        Console.WriteLine($"Total items consumed per second: {end - start:N0}");
                    }
                });
            
                await foreach(var item in c.ReadAllAsync())
                {
                    consumed++;
                    // Console.WriteLine(item);
                }
            }

            public async IAsyncEnumerable<T> ReadAllAsync()
            {
                while (await WaitToReadAsync())
                {
                    if (TryRead(out var item))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
