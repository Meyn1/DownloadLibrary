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
    /// A Class to hold the options on a <see cref="Request"/> class and to modify it.
    /// </summary>
    public class RequestOptions
    {
        ///// <summary>
        ///// If the <see cref="Request"/> has priority over other not prioritized <see cref="Request">Requests</see>.
        ///// </summary>
        //public bool HasPriority { get; init; } = false;
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
        public int DeployDelay = 0;
        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second  <see cref="Thread"/>.
        /// </summary>
        public RequestHandler RequestHandler { internal get; set; } = RequestHandler.MainRequestHandlers[0];
        /// <summary>
        /// How often the <see cref="Request"/> sould be retried if it fails.
        /// </summary>
        public byte TryCounter { get; set; } = 3;
        /// <summary>
        /// <see cref="System.Threading.CancellationToken"/> that the user sets to cancel the <see cref="Request"/>.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; } = null;
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> finished.
        /// </summary>
        public Action<object?>? CompleatedAction { get; set; }
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> failed.
        /// </summary>
        public Action<HttpResponseMessage>? FaultedAction { get; set; }
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> is cancelled.
        /// </summary>
        public Action? CancelledAction { get; set; }

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
            CompleatedAction = (Action<object?>?)options.CompleatedAction?.Clone();
            FaultedAction = (Action<HttpResponseMessage>?)options.FaultedAction?.Clone();
            CancelledAction = (Action?)options.CancelledAction?.Clone();
        }
    }
}
