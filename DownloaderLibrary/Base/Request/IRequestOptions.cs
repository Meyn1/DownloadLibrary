namespace DownloaderLibrary.Base.Request
{
    /// <summary>
    /// Generic interface that conatains design for all <see cref="RequestObject"/> types.
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if compleated</typeparam>
    /// <typeparam name="TFailed">Type of return if failed</typeparam>
    public interface IRequestOptions<TCompleated, TFailed>
    {
        /// <summary>
        /// If the Request sould be automaticly started if when it is inizialised.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// If the <see cref="RequestObject"/> has priority over other not prioritized <see cref="RequestObject">Requests</see>.
        /// </summary>
        public RequestPriority Priority { get; set; }

        /// <summary>
        /// Delays the start of the <see cref="RequestObject"/> on every start call for the specified number of milliseconds.
        /// </summary>
        public TimeSpan? DeployDelay { get; set; }

        /// <summary>
        /// If the <see cref="RequestObject"/> is an big file and should download in a second <see cref="Thread"/>.
        /// </summary>
        public RequestHandler Handler { get; set; }

        /// <summary>
        /// How often the <see cref="RequestObject"/> should be retried if it fails.
        /// </summary>
        public byte TryCounter { get; set; }

        /// <summary>
        /// How long sould be the new attemp be delayed if the <see cref="RequestObject"/> fails.
        /// </summary>
        public TimeSpan? DelayBetweenAttemps { get; set; }

        /// <summary>
        /// <see cref="System.Threading.CancellationToken"/> that the user sets to cancel the <see cref="RequestObject"/>.
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }

        /// <summary>
        /// Event that will be risen when the <see cref="RequestObject"/> is cancelled.
        /// </summary>
        public NotifyVoid? RequestCancelled { get; set; }

        /// <summary>
        /// Event that will be risen when the <see cref="RequestObject"/> is started.
        /// </summary>
        public NotifyVoid? RequestStarted { get; set; }

        /// <summary>
        /// Event that will be risen when the <see cref="RequestObject"/> finished.
        /// </summary>
        public Notify<TCompleated>? RequestCompleated { get; set; }

        /// <summary>
        /// Event that will be risen when the <see cref="RequestObject"/> failed.
        /// </summary>
        public Notify<TFailed>? RequestFailed { get; set; }
    }
}
