using DownloaderLibrary.Requests;
using System.Threading.Channels;

namespace DownloaderLibrary.Utilities
{
    /// <summary>Provides internal helper methods for implementing channels.</summary>
    internal static class ChannelUtilities
    {
        /// <summary>Sentinel object used to indicate being done writing.</summary>
        internal static readonly Exception s_doneWritingSentinel = new(nameof(s_doneWritingSentinel));
        /// <summary>A cached task with a Boolean true result.</summary>
        internal static readonly Task<bool> s_trueTask = Task.FromResult(result: true);
        /// <summary>A cached task with a Boolean false result.</summary>
        internal static readonly Task<bool> s_falseTask = Task.FromResult(result: false);
        /// <summary>A cached task that never completes.</summary>
        internal static readonly Task s_neverCompletingTask = new TaskCompletionSource<bool>().Task;

        /// <summary>Completes the specified TaskCompletionSource.</summary>
        /// <param name="tcs">The source to complete.</param>
        /// <param name="error">
        /// The optional exception with which to complete.
        /// If this is null or the DoneWritingSentinel, the source will be completed successfully.
        /// If this is an OperationCanceledException, it'll be completed with the exception's token.
        /// Otherwise, it'll be completed as faulted with the exception.
        /// </param>
        internal static void Complete(TaskCompletionSource tcs, Exception? error = null)
        {
            if (error is OperationCanceledException oce)
            {
                tcs.TrySetCanceled(oce.CancellationToken);
            }
            else if (error != null && error != s_doneWritingSentinel)
            {
                tcs.TrySetException(error);
            }
            else
            {
                tcs.TrySetResult();
            }
        }


        /// <summary>Gets a value task representing an error.</summary>
        /// <typeparam name="T">Specifies the type of the value that would have been returned.</typeparam>
        /// <param name="error">The error.  This may be <see cref="s_doneWritingSentinel"/>.</param>
        /// <returns>The failed task.</returns>
        internal static ValueTask<T> GetInvalidCompletionValueTask<T>(Exception error) => new(error == s_doneWritingSentinel ? Task.FromException<T>(CreateInvalidCompletionException()) :
                error is OperationCanceledException oce ? Task.FromCanceled<T>(oce.CancellationToken.IsCancellationRequested ? oce.CancellationToken : new CancellationToken(true)) :
                Task.FromException<T>(CreateInvalidCompletionException(error)));


        internal static void QueueWaiter(ref AsyncOperation<bool>? tail, AsyncOperation<bool> waiter)
        {
            AsyncOperation<bool>? c = tail;
            if (c == null)
            {
                waiter.Next = waiter;
            }
            else
            {
                waiter.Next = c.Next;
                c.Next = waiter;
            }
            tail = waiter;
        }

        internal static void WakeUpWaiters(ref AsyncOperation<bool>? listTail, bool result, Exception? error = null)
        {
            AsyncOperation<bool>? tail = listTail;
            if (tail != null)
            {
                listTail = null;

                AsyncOperation<bool> head = tail.Next!;
                AsyncOperation<bool> c = head;
                do
                {
                    AsyncOperation<bool> next = c.Next!;
                    c.Next = null;

                    bool completed = error != null ? c.TrySetException(error) : c.TrySetResult(result);

                    c = next;
                }
                while (c != head);
            }
        }

        /// <summary>Removes all operations from the queue, failing each.</summary>
        /// <param name="operations">The queue of operations to complete.</param>
        /// <param name="error">The error with which to complete each operations.</param>
        internal static void FailOperations<T, TInner>(Deque<T> operations, Exception error) where T : AsyncOperation<TInner>
        {
            while (!operations.IsEmpty)
            {
                operations.DequeueHead().TrySetException(error);
            }
        }

        /// <summary>Creates and returns an exception object to indicate that a channel has been closed.</summary>
        internal static Exception CreateInvalidCompletionException(Exception? inner = null) =>
            inner is OperationCanceledException ? inner :
            inner != null && inner != s_doneWritingSentinel ? new ChannelClosedException(inner) :
            new ChannelClosedException();


        /// <summary>
        /// Executes a parallel for-each operation on an Channel,
        /// enforcing a dynamic maximum degree of parallelism.
        /// </summary>
        /// <typeparam name="TSource">As Channel generic</typeparam>
        /// <param name="channel">Channel that is the source</param>
        /// <param name="body">Excecution function</param>
        /// <param name="options">A dynamic parallel option</param>
        /// <returns>A Task</returns>
        /// <exception cref="ArgumentNullException"></exception>
        internal static Task DynamicParallelForEachAsync<TSource>(this Channel<TSource> channel, Func<TSource, CancellationToken, ValueTask> body, DynamicParallelOptions? options = null)
        {
            _ = channel ?? throw new ArgumentNullException(nameof(channel));
            _ = body ?? throw new ArgumentNullException(nameof(body));
            options ??= new();

            SemaphoreSlim throttler = new(options.MaxDegreeOfParallelism);
            options.DegreeOfParallelismChangedDelta += OnParallismChanged;
            void OnParallismChanged(object? sender, int delta)
            {
                if (delta > 0)
                    throttler.Release(delta);
                else
                    for (int i = delta; i < 0; i++)
                        throttler.WaitAsync();
            }

            async IAsyncEnumerable<TSource> GetThrottledSource()
            {
                await foreach (TSource? element in channel.Reader.ReadAllAsync().WithCancellation(default).ConfigureAwait(false))
                {
                    if (options.EasyEndToken.IsCancellationRequested)
                    {
                        _ = channel.Writer.WriteAsync(element).AsTask();
                        break;
                    }
                    await throttler.WaitAsync().ConfigureAwait(false);
                    yield return element;
                }
            }

            return Parallel.ForEachAsync(GetThrottledSource(), options, async (item, ct) =>
            {
                try { await body(item, ct).ConfigureAwait(false); }
                finally { throttler.Release(); }
            }).ContinueWith(t =>
            {
                options.DegreeOfParallelismChangedDelta -= OnParallismChanged;
                return t;
            }, default, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default)
                .Unwrap();
        }
    }
}
