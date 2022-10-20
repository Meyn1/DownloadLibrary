using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace DownloaderLibrary.Utilities
{
    /// <summary>
    /// A implementation of channel with a priority listing
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PriorityChannel<T> : Channel<(int priority, T item)>
    {
        /// <summary>Task that indicates the channel has completed.</summary>
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>The items in the channel.</summary>
        private readonly ConcurrentQueue<(int priority, T item)>[] _queues = null!;
        /// <summary>Readers blocked reading from the channel.</summary>
        private readonly Deque<AsyncOperation<(int priority, T item)>> _blockedReaders = new();

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
            _queues = new ConcurrentQueue<(int priority, T item)>[_priorityCount];
            for (int i = 0; i < _priorityCount; i++)
                _queues[i] = new ConcurrentQueue<(int priority, T item)>();

            Reader = new UnboundedChannelReader(this);
            Writer = new PriorityChannelWriter(this);

        }

        private sealed class UnboundedChannelReader : ChannelReader<(int priority, T item)>
        {
            internal readonly PriorityChannel<T> _parent;
            private readonly AsyncOperation<(int priority, T item)> _readerSingleton;
            private readonly AsyncOperation<bool> _waiterSingleton;

            internal UnboundedChannelReader(PriorityChannel<T> parent)
            {
                _parent = parent;
                _readerSingleton = new AsyncOperation<(int priority, T item)>(true, pooled: true);
                _waiterSingleton = new AsyncOperation<bool>(true, pooled: true);
            }

            public override Task Completion => _parent._completion.Task;

            public override bool CanCount => true;

            public override bool CanPeek => true;

            public override int Count => _parent.m_count;

            public override ValueTask<(int priority, T item)> ReadAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                    return new ValueTask<(int priority, T item)>(Task.FromCanceled<(int priority, T item)>(cancellationToken));

                PriorityChannel<T> parent = _parent;
                for (int i = 0; i < parent._priorityCount; i++)
                    if (parent._queues[i].TryDequeue(out (int priority, T item) item))
                    {
                        Interlocked.Decrement(ref parent.m_count);
                        CompleteIfDone(parent);
                        return new ValueTask<(int priority, T item)>(item);
                    }

                lock (parent.SyncObj)
                {
                    for (int i = 0; i < parent._priorityCount; i++)
                        if (parent._queues[i].TryDequeue(out (int priority, T item) item))
                        {
                            Interlocked.Decrement(ref parent.m_count);
                            CompleteIfDone(parent);
                            return new ValueTask<(int priority, T item)>(item);
                        }

                    if (parent._doneWriting != null)
                        return ChannelUtilities.GetInvalidCompletionValueTask<(int priority, T item)>(parent._doneWriting);

                    if (!cancellationToken.CanBeCanceled)
                    {
                        AsyncOperation<(int priority, T item)> singleton = _readerSingleton;
                        if (singleton.TryOwnAndReset())
                        {
                            parent._blockedReaders.EnqueueTail(singleton);
                            return singleton.ValueTaskOfT;
                        }
                    }

                    AsyncOperation<(int priority, T item)> reader = new(true, cancellationToken);
                    parent._blockedReaders.EnqueueTail(reader);
                    return reader.ValueTaskOfT;
                }
            }

            public override bool TryRead([MaybeNullWhen(false)] out (int priority, T item) item)
            {
                PriorityChannel<T> parent = _parent;

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

            public override bool TryPeek([MaybeNullWhen(false)] out (int priority, T item) item)
            {
                PriorityChannel<T> parent = _parent;
                for (int i = 0; i < _parent._priorityCount; i++)
                    if (parent._queues[i].TryPeek(out item))
                        return true;
                item = new();
                return false;
            }

            private static void CompleteIfDone(PriorityChannel<T> parent)
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


                PriorityChannel<T> parent = _parent;

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

        private sealed class PriorityChannelWriter : ChannelWriter<(int priority, T item)>
        {
            internal readonly PriorityChannel<T> _parent;
            internal PriorityChannelWriter(PriorityChannel<T> parent) => _parent = parent;

            public override bool TryComplete(Exception? error)
            {
                PriorityChannel<T> parent = _parent;
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

                ChannelUtilities.FailOperations<AsyncOperation<(int priority, T item)>, (int priority, T item)>(parent._blockedReaders, ChannelUtilities.CreateInvalidCompletionException(error));
                ChannelUtilities.WakeUpWaiters(ref parent._waitingReadersTail, result: false, error: error);

                return true;
            }

            public override bool TryWrite((int priority, T item) pair)
            {
                PriorityChannel<T> parent = _parent;
                while (true)
                {
                    AsyncOperation<(int priority, T item)>? blockedReader = null;
                    AsyncOperation<bool>? waitingReadersTail = null;
                    lock (parent.SyncObj)
                    {

                        if (parent._doneWriting != null)
                            return false;


                        if (parent._blockedReaders.IsEmpty)
                        {
                            parent._queues[pair.priority].Enqueue(pair);
                            waitingReadersTail = parent._waitingReadersTail;
                            if (waitingReadersTail == null)
                                return true;

                            parent._waitingReadersTail = null;
                            Interlocked.Increment(ref parent.m_count);
                        }
                        else
                            blockedReader = parent._blockedReaders.DequeueHead();

                    }

                    if (blockedReader != null)
                    {
                        if (blockedReader.TrySetResult(pair))
                            return true;
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

            public override ValueTask WriteAsync((int priority, T item) item, CancellationToken cancellationToken) =>
                cancellationToken.IsCancellationRequested ? new ValueTask(Task.FromCanceled(cancellationToken)) :
                TryWrite(item) ? default :
                new ValueTask(Task.FromException(ChannelUtilities.CreateInvalidCompletionException(_parent._doneWriting)));



        }
        /// <summary>
        /// Creates a Array out the actual members of this Channel
        /// </summary>
        /// <returns>A Array T</returns>
        public (int priority, T item)[] ToArray()
        {
            (int priority, T item)[] result;

            lock (_queues)
            {
                result = new (int priority, T item)[Reader.Count];
                int index = 0;
                foreach (ConcurrentQueue<(int priority, T item)> q in _queues)
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
