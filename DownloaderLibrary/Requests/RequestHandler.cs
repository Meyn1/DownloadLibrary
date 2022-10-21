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

        private readonly PriorityChannel<Request> _requestsToPerform = new(3);
        /// <summary>
        /// Indicates if the <see cref="RequestHandler"/>.is running
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// Sets the max degreee of parallel requests that will be handled. 
        /// Default is null the DownloadHanlder will handle it automaticly.
        /// </summary>
        public byte? MaxDegreeOfParallelism
        {
            get => _maxDegreeOfParallelism; set
            {
                _parallelOptions.MaxDegreeOfParallelism = value ?? Math.Min(Math.Max(AutoParallelism, 2), MaxParallelism);
                _maxDegreeOfParallelism = value;
            }
        }
        private byte? _maxDegreeOfParallelism = null;
        private readonly DynamicParallelOptions _parallelOptions = new();

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
                if (_speedQuene.Count < 10)
                    return _speed;
                _speed = (long)_speedQuene.Average();
                _speedQuene.Clear();
                return _speed;
            }
        }
        /// <summary>
        /// Requests that are not yet Handeled
        /// </summary>
        public int CountRequests => _requestsToPerform.Reader.Count;

        /// <summary>
        /// Constructor for <see cref="RequestHandler"/> class.
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public RequestHandler(params Request[] requests) => AddRequest(requests);

        /// <summary>
        /// Adds Requests to the handler
        /// </summary>
        /// <param name="request">Requests that sould be added</param>
        public void AddRequest(Request request)
        => _ = _requestsToPerform.Writer.WriteAsync(new((int)request.Options.PriorityLevel, request)).AsTask();


        /// <summary>
        /// Adds Requests to the handler
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public void AddRequest(params Request[] requests)
        => Array.ForEach(requests, request => _ = _requestsToPerform.Writer.WriteAsync(new((int)request.Options.PriorityLevel, request)).AsTask());


        /// <summary>
        /// Runs the Request and adds Requests
        /// </summary>
        /// <param name="request">Requests that sould be added</param>
        public void RunRequests(Request request)
        {
            AddRequest(request);
            RunRequests();
        }

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
                _parallelOptions.CancellationToken = CT;
                if (CountRequests > 0)
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
            if (MaxDegreeOfParallelism == null)
                _parallelOptions.MaxDegreeOfParallelism = Math.Min(Math.Max(AutoParallelism, 2), MaxParallelism);
        }
        /// <summary>
        /// Runs the Request if it is not running
        /// </summary>
        public void RunRequests()
        {
            if (IsRunning || CT.IsCancellationRequested)
                return;
            IsRunning = true;

            Task.Run(async () =>
            {
                await _requestsToPerform.DynamicParallelForEachAsync(async (pair, ct) =>
                {
                    Request request = pair.item;

                    await request.StartRequestAsync();

                    if (request.State == RequestState.Compleated || request.State == RequestState.Failed || request.State == RequestState.Cancelled)
                        request.Dispose();
                    else if (request.State == RequestState.Available)
                        await _requestsToPerform.Writer.WriteAsync(pair);

                }, _parallelOptions);
                IsRunning = false;
                if (_requestsToPerform.Reader.Count != 0)
                    RunRequests();
            });
        }
    }
}
