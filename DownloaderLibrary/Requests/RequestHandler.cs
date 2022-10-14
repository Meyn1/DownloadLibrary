using DownloaderLibrary.Utilities;
using System.Collections.Concurrent;

namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Class that executes the Requests
    /// </summary>
    public class RequestHandler
    {
        /// <summary>
        /// Main Instance of the <see cref="HttpClient"/> that will be used to handle all <see cref="Request.Request"/> objects.
        /// </summary>
        public static HttpClient HttpClient { get; set; } = new();

        private readonly BlockingCollection<KeyValuePair<int, Request>> _requestsToPerform = new(new Utilities.PriorityQueue<int, Request>(3));
        /// <summary>
        /// Indicates if the <see cref="RequestHandler"/>.is running
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// Sets the max degreee of parallel requests that will be handled. 
        /// Default is null the DownloadHanlder will handle it automaticly.
        /// </summary>
        public byte? MaxDegreeOfParallelism { get; set; } = null;

        private int AutoParallelism => (int)(Environment.ProcessorCount * IOManager.BytesToMegabytes(Speed));
        private static int MaxParallelism => (int)(Environment.ProcessorCount / 1.7f);
        private readonly ConcurrentQueue<int> _speedQuene = new();
        private long _speed = 1024 * 1024;

        private CancellationTokenSource _cancellationTokenSource = new();
        /// <summary>
        /// Main <see cref="CancellationToken"/> for all <see cref="Request.Request"/>s.
        /// </summary>
        public CancellationToken CT => _cancellationTokenSource.Token;

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
        /// <summary>
        /// Requests that are not yet Handeled
        /// </summary>
        public int CountRequests => _requestsToPerform.Count;

        /// <summary>
        /// Constructor for <see cref="RequestHandler"/> class.
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public RequestHandler(params Request[] requests) => AddRequest(requests);

        /// <summary>
        /// Adds Requests to the handler
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public void AddRequest(params Request[] requests)
        => Array.ForEach(requests, request => _requestsToPerform.Add(new((int)request.Options.PriorityLevel, request)));

        /// <summary>
        /// Runs the Request and adds Requests
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public void RunRequests(params Request[] requests)
        {
            AddRequest(requests);
            RunRequests();
        }
        internal static RequestHandler[] MainRequestHandlers { get; } = new RequestHandler[] { new(), new() };

        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> if the old one was canceled.
        /// </summary>
        public void CreateCTS()
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                if (CountRequests != 0)
                    RunRequests();
            }
        }
        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> for all main RequestHandlers
        /// </summary>
        public static void CreateMainCTS()
        {
            foreach (RequestHandler handler in MainRequestHandlers)
                handler.CreateCTS();
        }

        /// <summary>
        /// Cancels the main <see cref="CancellationTokenSource"/> for all Requests in this RequestHandler.
        /// </summary>
        public void CancelCTS() => _cancellationTokenSource?.Cancel();

        /// <summary>
        /// Cancels the main <see cref="CancellationTokenSource"/> for all Requests in the Main RequestHandlers.
        /// </summary>
        public static void CancelMainCTS()
        {
            foreach (RequestHandler handler in MainRequestHandlers)
                handler.CancelCTS();
        }

        /// <summary>
        /// Adds the writen bytes to the RequestHandler
        /// </summary>
        /// <param name="bytesPerSecond">Written bytes per secons</param>
        public void AddSpeed(int bytesPerSecond)
        {
            if (_speedQuene.Count > 20)
                _speedQuene.TryDequeue(out _);
            _speedQuene.Enqueue(bytesPerSecond);
        }
        /// <summary>
        /// Runs the Request if it is not running
        /// </summary>
        public void RunRequests()
        {
            if (IsRunning || CT.IsCancellationRequested)
                return;
            IsRunning = true;

            Task.Run(() =>
            {
                int i = 0;
                Parallel.ForEach(_requestsToPerform.GetConsumingPartitioner(), new ParallelOptions() { MaxDegreeOfParallelism = MaxDegreeOfParallelism ?? Math.Min(Math.Max(AutoParallelism, 2), MaxParallelism) },
                    (pair, state) =>
                    {
                        Request request = pair.Value;
                        request.RunRequest();
                        if (request.State == RequestState.Compleated || request.State == RequestState.Failed)
                            request.Dispose();
                        else if (request.State == RequestState.Onhold)
                            _requestsToPerform.Add(pair);
                        else if (request.State == RequestState.Available)
                            _requestsToPerform.Add(pair);

                        if (CT.IsCancellationRequested)
                            state.Stop();
                        if (i++ > 50 && _requestsToPerform.Count == 0)
                            state.Break();
                    });
                IsRunning = false;
                if (_requestsToPerform.Count != 0)
                    RunRequests();
            });

        }
    }
}
