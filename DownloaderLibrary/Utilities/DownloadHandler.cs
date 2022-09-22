using DownloaderLibrary.Request;
using System.Collections.Concurrent;

namespace DownloaderLibrary.Utilities
{
    /// <summary>
    /// A Class to handle all <see cref="Request.Request"/> object with an static instance of <see cref="HttpClient"/>.
    /// </summary>
    public class DownloadHandler : HttpClient
    {
        /// <summary>
        /// Main Instance of the <see cref="HttpClient"/> that will be used to handle all <see cref="Request.Request"/> objects.
        /// </summary>
        public static DownloadHandler Instance { get; private set; } = new();

        private CancellationTokenSource _mainCancellationTokenSource = new();
        /// <summary>
        /// Main <see cref="CancellationToken"/> for all <see cref="Request.Request"/>s.
        /// </summary>
        public CancellationToken MainCT => _mainCancellationTokenSource.Token;

        private readonly BlockingCollection<KeyValuePair<int, Request.Request>> _requestsToPerform = new(new PriorityQueue<int, Request.Request>(2));
        private readonly BlockingCollection<KeyValuePair<int, Request.Request>> _downloadsToPerform = new(new PriorityQueue<int, Request.Request>(2));

        private int NormRequests => (int)(Environment.ProcessorCount * IOManager.BytesToMegabytes(Speed));
        private static int MaxRequests => (int)(Environment.ProcessorCount / 1.5f);
        private const int minRequests = 2;
        private readonly ConcurrentQueue<int> _speedQuene = new();
        private long _speed = 1024 * 1024;
        /// <summary>
        /// Gets the speed of the downloads of the LoadRequests.
        /// Includes write time to disc.
        /// </summary>
        public long Speed
        {
            get
            {
                if (_speedQuene.IsEmpty)
                    return _speed;
                _speed = 0;
                int counter = _speedQuene.Count;
                foreach (int speed in _speedQuene)
                    _speed += speed;
                _speed /= counter;
                _speedQuene.Clear();
                return _speed;
            }
        }
        private volatile bool isDownloading = false;
        private volatile bool isLoading = false;

        /// <summary>
        /// Cancels the main <see cref="CancellationTokenSource"/> for all Requests.
        /// </summary>
        public void CancelCTS() => _mainCancellationTokenSource?.Cancel();
        internal void Add(Request.Request request)
        {
            if (request.Options.IsDownload)
            {
                _downloadsToPerform.Add(new(request.Options.HasPriority ? 0 : 1, request)); PerformDownloads();
                return;
            }
            _requestsToPerform.Add(new(request.Options.HasPriority ? 0 : 1, request)); PerformRequests();
        }

        private DownloadHandler()
        {
        }

        internal void AddSpeed(int bytesPerSecond)
        {
            if (_speedQuene.Count > 20)
                _speedQuene.TryDequeue(out _);
            _speedQuene.Enqueue(bytesPerSecond);
        }


        private void PerformRequests()
        {

            if (isLoading || MainCT.IsCancellationRequested)
                return;
            isLoading = true;

            Task.Run(() =>
            {
                int i = 0;

                Parallel.ForEach(_requestsToPerform.GetConsumingPartitioner(), new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(Math.Max(NormRequests, minRequests), MaxRequests) },
                    (pair, state) =>
                    {

                        Request.Request request = pair.Value;
                        request.RunRequest();
                        if (request.State == RequestState.Compleated || request.State == RequestState.Failed)
                            request.Dispose();
                        else if (request.State == RequestState.Onhold)
                            _requestsToPerform.Add(pair);
                        else if (request.State == RequestState.Available)
                            _requestsToPerform.Add(pair);

                        if (_mainCancellationTokenSource.IsCancellationRequested)
                        {
                            state.Stop();

                            isLoading = false;
                            return;
                        }

                        if (i++ > 50 && _requestsToPerform.Count == 0)
                        {
                            state.Break();

                            isLoading = false;
                        }
                    });

            });

        }

        private void PerformDownloads()
        {
            if (isDownloading || MainCT.IsCancellationRequested)
                return;
            isDownloading = true;
            Task.Run(() =>
            {
                int i = 0;
                Parallel.ForEach(_downloadsToPerform.GetConsumingPartitioner(), new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(Math.Max(NormRequests, minRequests), MaxRequests) },
                   (pair, state) =>
                   {
                       Request.Request request = pair.Value;
                       request.RunRequest();
                       if (request.State == RequestState.Compleated || request.State == RequestState.Failed)
                           request.Dispose();
                       else if (request.State == RequestState.Onhold)
                           _downloadsToPerform.Add(pair);
                       else if (request.State == RequestState.Available)
                           _downloadsToPerform.Add(pair);


                       if (_mainCancellationTokenSource.IsCancellationRequested)
                       {
                           state.Stop();
                           isDownloading = false;
                           return;
                       }

                       if (i++ > 50 && _downloadsToPerform.Count == 0)
                       {
                           state.Break();
                           isDownloading = false;
                       }
                   });

            });

        }
        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> if the old one was canceled.
        /// </summary>
        public void CreateCTS()
        {
            if (_mainCancellationTokenSource.IsCancellationRequested)
            {
                _mainCancellationTokenSource.Dispose();
                _mainCancellationTokenSource = new CancellationTokenSource();
                if (_downloadsToPerform.Count != 0)
                    PerformDownloads();
                if (_requestsToPerform.Count != 0)
                    PerformRequests();
            }
        }
    }
}
