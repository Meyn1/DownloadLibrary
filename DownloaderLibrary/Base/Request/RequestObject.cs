namespace DownloaderLibrary.Base.Request
{

    /// <summary>
    /// A <see cref="RequestObject"/> object that can be managed by the <see cref="RequestHandler"/>.
    /// </summary>
    public abstract class RequestObject : IDisposable
    {
        /// <summary>
        /// Main Instance of <see cref="System.Net.Http.HttpClient"/> that will be used to handle HttpRequests the <see cref="RequestObject"/> that are using it.
        /// </summary>
        public static HttpClient HttpClient
        {
            get
            {
                if (_httpClient != null)
                    return _httpClient;
                _httpClient = new();
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");
                return _httpClient;

            }
            set => _httpClient = value;
        }



        private static HttpClient? _httpClient = null;

        /// <summary>
        /// The <see cref="RequestState"/> of this <see cref="RequestObject"/>.
        /// </summary>
        public abstract RequestState State { get; protected set; }

        /// <summary>
        /// If the <see cref="RequestObject"/> has priority over other not prioritized <see cref="RequestObject">Requests</see>.
        /// </summary>
        public abstract RequestPriority Priority { get; }

        /// <summary>
        /// <see cref="System.Threading.Tasks.Task"/> that indicates of this <see cref="RequestObject"/> finished.
        /// </summary>
        public abstract Task Task { get; }

        /// <summary>
        /// Delays the start of the <see cref="RequestObject"/> on every Start call for the specified number of milliseconds.
        /// </summary>
        public abstract TimeSpan? DeployDelay { get; set; }

        /// <summary>
        /// Runs the <see cref="RequestObject"/> that was created out this object
        /// </summary>
        internal abstract Task StartRequestAsync();

        /// <summary>
        /// Cancel the <see cref="RequestObject"/>
        /// </summary>
        public abstract void Cancel();

        /// <summary>
        /// Start the <see cref="RequestObject"/> if it is not yet started or paused.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Set the <see cref="RequestObject"/> on hold.
        /// </summary>
        public abstract void Pause();

        /// <summary>
        /// Dispose the <see cref="RequestObject"/>.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Wait to finish this <see cref="RequestObject"/>.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Wait() => Task.Wait();

    }
}