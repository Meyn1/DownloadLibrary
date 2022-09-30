using System.Collections;
using System.Collections.Concurrent;

namespace DownloaderLibrary.Utilities
{
    // Implementation of a priority queue that has bounding and blocking functionality.
    internal class PriorityQueue<TPriority, TValue> : IProducerConsumerCollection<KeyValuePair<int, TValue>>
    {
        // Each internal queue in the array represents a priority level.
        // All elements in a given array share the same priority.
        private readonly ConcurrentQueue<KeyValuePair<int, TValue>>[] _queues = null!;

        // The number of queues we store internally.
        private readonly int _priorityCount = 0;
        private int m_count = 0;

        public PriorityQueue(int priCount)
        {
            _priorityCount = priCount;
            _queues = new ConcurrentQueue<KeyValuePair<int, TValue>>[_priorityCount];
            for (int i = 0; i < _priorityCount; i++)
                _queues[i] = new ConcurrentQueue<KeyValuePair<int, TValue>>();
        }

        // IProducerConsumerCollection members
        public bool TryAdd(KeyValuePair<int, TValue> item)
        {
            _queues[item.Key].Enqueue(item);
            Interlocked.Increment(ref m_count);
            return true;
        }

        public bool TryTake(out KeyValuePair<int, TValue> item)
        {
            // Loop through the queues in priority order
            // looking for an item to dequeue.
            for (int i = 0; i < _priorityCount; i++)

                // Lock the internal data so that the Dequeue
                // operation and the updating of m_count are atomic.
                lock (_queues)
                {
                    bool success = _queues[i].TryDequeue(out item);
                    if (success)
                    {
                        Interlocked.Decrement(ref m_count);
                        return true;
                    }
                }


            // If we get here, we found nothing.
            // Assign the out parameter to its default value and return false.
            item = new KeyValuePair<int, TValue>(0, default(TValue)!);
            return false;
        }

        public int Count => m_count;

        // Required for ICollection
        void ICollection.CopyTo(Array array, int index) =>
            CopyTo((KeyValuePair<int, TValue>[])array, index);


        // CopyTo is problematic in a producer-consumer.
        // The destination array might be shorter or longer than what
        // we get from ToArray due to adds or takes after the destination array was allocated.
        // Therefore, all we try to do here is fill up destination with as much
        // data as we have without running off the end.
        public void CopyTo(KeyValuePair<int, TValue>[] destination, int destStartingIndex)
        {
            _ = destination ?? throw new ArgumentNullException();
            if (destStartingIndex < 0) throw new ArgumentOutOfRangeException();
            KeyValuePair<int, TValue>[] temp = ToArray();
            for (int i = 0; i < destination.Length && i < temp.Length; i++)
                destination[i] = temp[i];
        }

        public KeyValuePair<int, TValue>[] ToArray()
        {
            KeyValuePair<int, TValue>[] result;

            lock (_queues)
            {
                result = new KeyValuePair<int, TValue>[Count];
                int index = 0;
                foreach (ConcurrentQueue<KeyValuePair<int, TValue>> q in _queues)
                    if (q.Count > 0)
                    {
                        q.CopyTo(result, index);
                        index += q.Count;
                    }
                return result;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        public IEnumerator<KeyValuePair<int, TValue>> GetEnumerator()
        {
            for (int i = 0; i < _priorityCount; i++)
                foreach (KeyValuePair<int, TValue> item in _queues[i])
                    yield return item;
        }

        public bool IsSynchronized => throw new NotSupportedException();
        public object SyncRoot => throw new NotSupportedException();

    }
}
