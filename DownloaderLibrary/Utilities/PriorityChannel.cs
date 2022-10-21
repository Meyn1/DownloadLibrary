using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace DownloaderLibrary.Utilities
{
    /// <summary>
    /// A implementation of channel with a priority listing
    /// </summary>
    /// <typeparam name="TElement"></typeparam>
    public class PriorityChannel<TElement> : Channel<(int priority, TElement item)>
    {
        /// <summary>Task that indicates the channel has completed.</summary>
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>The items in the channel.</summary>
        private readonly ConcurrentQueue<(int priority, TElement item)>[] _queues = null!;
        /// <summary>Readers blocked reading from the channel.</summary>
        private readonly Deque<AsyncOperation<(int priority, TElement item)>> _blockedReaders = new();

        /// <summary>Readers waiting for a notification that data is available.</summary>
        private AsyncOperation<bool>? _waitingReadersTail;
        /// <summary>Set to non-null once Complete has been called.</summary>
        private Exception? _doneWriting;

        // The number of queues we store internally.
        private readonly int _priorityCount = 0;
        private int m_count = 0;

        /// <summary>
        /// Initialize the priority channel.
        /// </summary>
        /// <param name="priCount">How many prioritys the channel sould handle</param>
        internal PriorityChannel(int priCount)
        {
            _priorityCount = priCount;
            _queues = new ConcurrentQueue<(int priority, TElement item)>[_priorityCount];
            for (int i = 0; i < _priorityCount; i++)
                _queues[i] = new ConcurrentQueue<(int priority, TElement item)>();

            Reader = new PriorityChannelReader(this);
            Writer = new PriorityChannelWriter(this);

        }

        private sealed class PriorityChannelReader : ChannelReader<(int priority, TElement item)>
        {
            internal readonly PriorityChannel<TElement> _parent;
            private readonly AsyncOperation<(int priority, TElement item)> _readerSingleton;
            private readonly AsyncOperation<bool> _waiterSingleton;

            internal PriorityChannelReader(PriorityChannel<TElement> parent)
            {
                _parent = parent;
                _readerSingleton = new AsyncOperation<(int priority, TElement item)>(true, pooled: true);
                _waiterSingleton = new AsyncOperation<bool>(true, pooled: true);
            }

            public override Task Completion => _parent._completion.Task;

            public override bool CanCount => true;

            public override bool CanPeek => true;

            public override int Count => _parent.m_count;

            public override ValueTask<(int priority, TElement item)> ReadAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new ValueTask<(int priority, TElement item)>(Task.FromCanceled<(int priority, TElement item)>(cancellationToken));

                PriorityChannel<TElement> parent = _parent;
                for (int i = 0; i < parent._priorityCount; i++)
                    if (parent._queues[i].TryDequeue(out (int priority, TElement item) item))
                    {
                        Interlocked.Decrement(ref parent.m_count);
                        CompleteIfDone(parent);
                        return new ValueTask<(int priority, TElement item)>(item);
                    }

                lock (parent.SyncObj)
                {
                    for (int i = 0; i < parent._priorityCount; i++)
                        if (parent._queues[i].TryDequeue(out (int priority, TElement item) item))
                        {
                            Interlocked.Decrement(ref parent.m_count);
                            CompleteIfDone(parent);
                            return new ValueTask<(int priority, TElement item)>(item);
                        }

                    if (parent._doneWriting != null)
                        return ChannelUtilities.GetInvalidCompletionValueTask<(int priority, TElement item)>(parent._doneWriting);

                    if (!cancellationToken.CanBeCanceled)
                    {
                        AsyncOperation<(int priority, TElement item)> singleton = _readerSingleton;
                        if (singleton.TryOwnAndReset())
                        {
                            parent._blockedReaders.EnqueueTail(singleton);
                            return singleton.ValueTaskOfT;
                        }
                    }

                    AsyncOperation<(int priority, TElement item)> reader = new(true, cancellationToken);
                    parent._blockedReaders.EnqueueTail(reader);
                    return reader.ValueTaskOfT;
                }
            }

            public override bool TryRead([MaybeNullWhen(false)] out (int priority, TElement item) item)
            {
                PriorityChannel<TElement> parent = _parent;

                for (int i = 0; i < parent._priorityCount; i++)
                    if (parent._queues[i].TryDequeue(out item))
                    {
                        Interlocked.Decrement(ref parent.m_count);
                        CompleteIfDone(parent);
                        return true;
                    }

                item = default;
                return false;
            }

            public override bool TryPeek([MaybeNullWhen(false)] out (int priority, TElement item) item)
            {
                PriorityChannel<TElement> parent = _parent;
                for (int i = 0; i < _parent._priorityCount; i++)
                    if (parent._queues[i].TryPeek(out item))
                        return true;
                item = new();
                return false;
            }

            private static void CompleteIfDone(PriorityChannel<TElement> parent)
            {
                if (parent._doneWriting != null && parent._queues.All(x => x.IsEmpty))
                    ChannelUtilities.Complete(parent._completion, parent._doneWriting);

            }

            public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));


                if (_parent._queues.Any(x => !x.IsEmpty))
                    return new ValueTask<bool>(true);


                PriorityChannel<TElement> parent = _parent;

                lock (parent.SyncObj)
                {
                    if (_parent._queues.Any(x => !x.IsEmpty))
                        return new ValueTask<bool>(true);

                    if (parent._doneWriting != null)
                        return parent._doneWriting != ChannelUtilities.s_doneWritingSentinel ?
                            new ValueTask<bool>(Task.FromException<bool>(parent._doneWriting)) :
                            default;

                    if (!cancellationToken.CanBeCanceled)
                    {
                        AsyncOperation<bool> singleton = _waiterSingleton;
                        if (singleton.TryOwnAndReset())
                        {
                            ChannelUtilities.QueueWaiter(ref parent._waitingReadersTail, singleton);
                            return singleton.ValueTaskOfT;
                        }
                    }

                    AsyncOperation<bool> waiter = new(true, cancellationToken);
                    ChannelUtilities.QueueWaiter(ref parent._waitingReadersTail, waiter);
                    return waiter.ValueTaskOfT;
                }
            }

        }

        private sealed class PriorityChannelWriter : ChannelWriter<(int priority, TElement item)>
        {
            internal readonly PriorityChannel<TElement> _parent;
            internal PriorityChannelWriter(PriorityChannel<TElement> parent) => _parent = parent;

            public override bool TryComplete(Exception? error)
            {
                PriorityChannel<TElement> parent = _parent;
                bool completeTask;

                lock (parent.SyncObj)
                {
                    if (parent._doneWriting != null)
                        return false;

                    parent._doneWriting = error ?? ChannelUtilities.s_doneWritingSentinel;
                    completeTask = parent._queues.All(x => x.IsEmpty);
                }
                if (completeTask)
                    ChannelUtilities.Complete(parent._completion, error);

                ChannelUtilities.FailOperations<AsyncOperation<(int priority, TElement item)>, (int priority, TElement item)>(parent._blockedReaders, ChannelUtilities.CreateInvalidCompletionException(error));
                ChannelUtilities.WakeUpWaiters(ref parent._waitingReadersTail, result: false, error: error);

                return true;
            }

            public override bool TryWrite((int priority, TElement item) pair)
            {
                PriorityChannel<TElement> parent = _parent;
                while (true)
                {
                    AsyncOperation<(int priority, TElement item)>? blockedReader = null;
                    AsyncOperation<bool>? waitingReadersTail = null;
                    lock (parent.SyncObj)
                    {

                        if (parent._doneWriting != null)
                            return false;


                        if (parent._blockedReaders.IsEmpty)
                        {
                            parent._queues[pair.priority].Enqueue(pair);
                            Interlocked.Increment(ref parent.m_count);
                            waitingReadersTail = parent._waitingReadersTail;
                            if (waitingReadersTail == null)
                                return true;

                            parent._waitingReadersTail = null;
                        }
                        else
                            blockedReader = parent._blockedReaders.DequeueHead();
                    }

                    if (blockedReader != null)
                    {
                        if (blockedReader.TrySetResult(pair))
                        {
                            Interlocked.Increment(ref parent.m_count);
                            return true;
                        }
                    }
                    else
                    {
                        ChannelUtilities.WakeUpWaiters(ref waitingReadersTail, result: true);
                        return true;
                    }
                }
            }

            public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken)
            {
                Exception? doneWriting = _parent._doneWriting;
                return
                    cancellationToken.IsCancellationRequested ? new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken)) :
                    doneWriting == null ? new ValueTask<bool>(true) : // unbounded writing can always be done if we haven't completed
                    doneWriting != ChannelUtilities.s_doneWritingSentinel ? new ValueTask<bool>(Task.FromException<bool>(doneWriting)) :
                    default;
            }

            public override ValueTask WriteAsync((int priority, TElement item) item, CancellationToken cancellationToken) =>
                cancellationToken.IsCancellationRequested ? new ValueTask(Task.FromCanceled(cancellationToken)) :
                TryWrite(item) ? default :
                new ValueTask(Task.FromException(ChannelUtilities.CreateInvalidCompletionException(_parent._doneWriting)));



        }
        /// <summary>
        /// Creates a Array out the actual members of this Channel
        /// </summary>
        /// <returns>A Array T</returns>
        public (int priority, TElement item)[] ToArray()
        {
            (int priority, TElement item)[] result;

            lock (_queues)
            {
                result = new (int priority, TElement item)[Reader.Count];
                int index = 0;
                foreach (ConcurrentQueue<(int priority, TElement item)> q in _queues)
                    if (q.Count > 0)
                    {
                        q.CopyTo(result, index);
                        index += q.Count;
                    }
                return result;
            }
        }



        /// <summary>Gets the object used to synchronize access to all state on this instance.</summary>
        private object SyncObj => _queues;
    }
}
