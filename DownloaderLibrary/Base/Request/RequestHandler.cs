using DownloaderLibrary.Utilities;
using System.Collections.Concurrent;

namespace DownloaderLibrary.Base.Request
{
    /// <summary>
    /// Class that executes the Requests
    /// </summary>
    public class RequestHandler
    {
        /// <summary>
        /// Priority Channel that holds all Requests
        /// </summary>
        private readonly PriorityChannel<RequestObject> _requestsChannel = new(3);

        /// <summary>
        /// Indicates if the <see cref="RequestHandler"/> is running.
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
                _requestsChannel.Options.MaxDegreeOfParallelism = value ?? AutoParallelism;
                _maxDegreeOfParallelism = value;
            }
        }
        private byte? _maxDegreeOfParallelism;

        private int AutoParallelism => Math.Min(Math.Max((int)(Environment.ProcessorCount * IOManager.BytesToMegabytes(Speed)), 2), MaxParallelism);
        private static int MaxParallelism => (int)(Environment.ProcessorCount * 1.7f);
        private readonly ConcurrentQueue<int> _speedQuene = new();

        private CancellationTokenSource _cts = new();
        private readonly PauseTokenSource _pts = new();
        /// <summary>
        /// Main <see cref="CancellationToken"/> for all <see cref="RequestObject.RequestObject"/>s.
        /// </summary>
        public CancellationToken CT => _cts.Token;

        /// <summary>
        /// Gets the speed of the downloads of the LoadRequests.
        /// Includes write time to disc.
        /// </summary>
        public long Speed
        {
            get
            {
                if (_speedQuene.Count < 10)
                    return 1024 * 1024;
                return (long)_speedQuene.Average();
            }
        }

        internal static RequestHandler[] MainRequestHandlers { get; } = new RequestHandler[] { new(), new() };

        /// <summary>
        /// Requests that are not yet Handeled
        /// </summary>
        public int CountRequests => _requestsChannel.Reader.Count;

        /// <summary>
        /// Constructor for <see cref="RequestHandler"/> class.
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public RequestHandler(params RequestObject[] requests)
        {
            AddRequest(requests);
            _requestsChannel.Options.EasyEndToken = _pts.Token;
            _requestsChannel.Options.MaxDegreeOfParallelism = AutoParallelism;
        }

        /// <summary>
        /// Adds Requests to the handler
        /// </summary>
        /// <param name="request">Requests that sould be added</param>
        public void AddRequest(RequestObject request)
        => _ = _requestsChannel.Writer.WriteAsync(new((int)request.Priority, request)).AsTask();


        /// <summary>
        /// Adds Requests to the handler
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public void AddRequest(params RequestObject[] requests)
        => Array.ForEach(requests, request => _ = _requestsChannel.Writer.WriteAsync(new((int)request.Priority, request)).AsTask());


        /// <summary>
        /// Runs the Request and adds Requests
        /// </summary>
        /// <param name="request">Requests that sould be added</param>
        public void RunRequests(RequestObject request)
        {
            AddRequest(request);
            RunRequests();
        }

        /// <summary>
        /// Runs the Request and adds Requests
        /// </summary>
        /// <param name="requests">Requests that sould be added</param>
        public void RunRequests(params RequestObject[] requests)
        {
            AddRequest(requests);
            RunRequests();
        }

        /// <summary>
        /// Resumes the handler if it was paused
        /// </summary>
        public void Resume()
        {
            if (!_requestsChannel.Options.EasyEndToken.IsPaused)
                return;
            _pts.Resume();
            if (CountRequests > 0)
                RunRequests();
        }

        /// <summary>
        /// Pause the handler.
        /// It lets running requests complete
        /// </summary>
        public void Pause() => _pts.Pause();

        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> if the old one was canceled.
        /// </summary>
        public void CreateCTS()
        {
            if (_cts.IsCancellationRequested)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
                _requestsChannel.Options.CancellationToken = CT;
                if (CountRequests > 0)
                    RunRequests();
            }
        }

        /// <summary>
        /// Creates a new <see cref="CancellationTokenSource"/> for all main RequestHandlers
        /// </summary>
        public static void CreateMainCTS() => Array.ForEach(MainRequestHandlers, handler => handler.CreateCTS());

        /// <summary>
        /// Cancels the main <see cref="CancellationTokenSource"/> for all Requests in this RequestHandler.
        /// </summary>
        public void CancelCTS() => _cts.Cancel();

        /// <summary>
        /// Cancels the main <see cref="CancellationTokenSource"/> for all Requests in the Main RequestHandlers.
        /// </summary>
        public static void CancelMainCTS() => Array.ForEach(MainRequestHandlers, handler => handler.CancelCTS());

        /// <summary>
        ///  Pause the handler for all Requests in the Main RequestHandlers.
        /// It lets running requests complete
        /// </summary>
        public static void PauseMain() => Array.ForEach(MainRequestHandlers, handler => handler.Pause());

        /// <summary>
        /// Resumes all Requests in the Main RequestHandlers if it was paused
        /// It lets running requests complete
        /// </summary>
        public static void ReusmeMain() => Array.ForEach(MainRequestHandlers, handler => handler.Resume());

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
                _requestsChannel.Options.MaxDegreeOfParallelism = AutoParallelism;
        }
        /// <summary>
        /// Runs the Request if it is not running
        /// </summary>
        public void RunRequests()
        {
            if (IsRunning || CT.IsCancellationRequested || _pts.IsPaused)
                return;
            IsRunning = true;
            Task.Run(async () => await RunChannel());
        }

        private async Task RunChannel()
        {
            await _requestsChannel.RunParallelReaderAsync(async (pair, ct) => await HandleRequests(pair));
            IsRunning = false;
            if (_requestsChannel.Reader.Count != 0)
                RunRequests();
        }

        private async Task HandleRequests(PriorityItem<RequestObject> pair)
        {
            RequestObject request = pair.Item;
            await request.StartRequestAsync();

            if (request.State is RequestState.Compleated or RequestState.Failed or RequestState.Cancelled)
                request.Dispose();
            else if (request.State == RequestState.Available)
                await _requestsChannel.Writer.WriteAsync(pair);
        }
    }
}
