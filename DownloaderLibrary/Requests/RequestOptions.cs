namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Enum to set the Priority of a Request;
    /// </summary>
    public enum PriorityLevel
    {
        /// <summary>
        /// Highest priority
        /// </summary>
        High,
        /// <summary>
        /// Normal priority
        /// </summary>
        Normal,
        /// <summary>
        /// Lowest priority
        /// </summary>
        Low
    }
    /// <summary>
    /// Delegate has no return type or parameter;
    /// </summary>
    public delegate void NotifyVoid();

    /// <summary>
    /// Delegate has no return type but a parameter;
    /// </summary>
    /// <param name="httpResponseMessage"></param>
    public delegate void NotifyMessage(HttpResponseMessage httpResponseMessage);

    /// <summary>
    /// Delegate has no return type but a parameter;
    /// </summary>
    /// <param name="obj"></param>
    public delegate void NotifyObject(object? obj);

    /// <summary>
    /// A Class to hold the options on a <see cref="Request"/> class and to modify it.
    /// </summary>
    public class RequestOptions
    {
        /// <summary>
        /// If the Request sould be automaticly started if when it is inizialised.
        /// </summary>
        public bool AutoStart { get; set; } = true;
        /// <summary>
        /// If the <see cref="Request"/> has priority over other not prioritized <see cref="Request">Requests</see>.
        /// </summary>
        public PriorityLevel PriorityLevel { get; set; } = PriorityLevel.Normal;

        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second <see cref="Thread"/>.
        /// Can't be used if <see cref="RequestHandler"/> is manually set.
        /// </summary>
        public bool IsDownload
        {
            get => _isDownload; set
            {
                _isDownload = value;
                if (value && RequestHandler == RequestHandler.MainRequestHandlers[0])
                    RequestHandler = RequestHandler.MainRequestHandlers[1];
            }
        }
        private bool _isDownload = false;

        /// <summary>
        /// Delays the start of the <see cref="Request"/> on every Start call for the specified number of milliseconds.
        /// </summary>
        public TimeSpan? DeployDelay { get; set; } = null;
        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second  <see cref="Thread"/>.
        /// </summary>
        public RequestHandler RequestHandler { internal get; set; } = RequestHandler.MainRequestHandlers[0];
        /// <summary>
        /// How often the <see cref="Request"/> sould be retried if it fails.
        /// </summary>
        public byte TryCounter { get; set; } = 3;

        /// <summary>
        /// How long sould be the new Attemp be delayed if the <see cref="Request"/> fails.
        /// </summary>
        public TimeSpan? DelayBetweenAttemps { get; set; } = null;

        /// <summary>
        /// <see cref="System.Threading.CancellationToken"/> that the user sets to cancel the <see cref="Request"/>.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; } = null;
        /// <summary>
        /// Event that will be risen when the <see cref="Request"/> finished.
        /// </summary>
        public NotifyObject? RequestCompleated;
        /// <summary>
        /// Event that will be risen when the <see cref="Request"/> failed.
        /// </summary>
        public NotifyMessage? RequestFailed;
        /// <summary>
        /// Event that will be risen when the <see cref="Request"/> is cancelled.
        /// </summary>
        public NotifyVoid? RequestCancelled;

        /// <summary>
        /// Main Constructor for a <see cref="RequestOptions"/> object.
        /// </summary>
        public RequestOptions() { }

        /// <summary>
        /// Creates a <see cref="RequestOptions"/> object baes on another <see cref="RequestOptions"/> object.
        /// </summary>
        /// <param name="options"><see cref="RequestOptions"/> parameter to copy.</param>
        /// <exception cref="ArgumentNullException">Throws exception if <see cref="RequestOptions"/> is null</exception>
        public RequestOptions(RequestOptions options)
        {
            _ = options ?? throw new ArgumentNullException(nameof(options));
            PriorityLevel = options.PriorityLevel;
            _isDownload = options.IsDownload;
            RequestHandler = options.RequestHandler;
            TryCounter = options.TryCounter;
            CancellationToken = options.CancellationToken;
            RequestCompleated += options.RequestCompleated;
            RequestFailed += options.RequestFailed;
            RequestCancelled += options.RequestCancelled;
        }
    }
}
