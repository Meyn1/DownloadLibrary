namespace DownloaderLibrary.Base.Request
{
    /// <summary>
    /// Class implementation of IRequestOptions
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if compleated</typeparam>
    /// <typeparam name="TFailed">Type of return if failed</typeparam>
    public class RequestOptions<TCompleated, TFailed> : IRequestOptions<TCompleated, TFailed>
    {
        /// <inheritdoc />
        public bool AutoStart { get; set; } = true;

        ///<inheritdoc />
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;

        ///<inheritdoc />
        public CancellationToken? CancellationToken { get; set; }

        ///<inheritdoc />
        public TimeSpan? DeployDelay { get; set; } = null;
        ///<inheritdoc />
        public RequestHandler Handler { get; set; } = RequestHandler.MainRequestHandlers[0];
        ///<inheritdoc />
        public byte TryCounter { get; set; } = 3;

        ///<inheritdoc />
        public TimeSpan? DelayBetweenAttemps { get; set; } = null;

        ///<inheritdoc />
        public NotifyVoid? RequestStarted { get; set; }

        ///<inheritdoc />
        public Notify<TCompleated>? RequestCompleated { get; set; }

        ///<inheritdoc />
        public Notify<TFailed>? RequestFailed { get; set; }

        ///<inheritdoc />
        public NotifyVoid? RequestCancelled { get; set; }

        /// <summary>
        /// Creates a copy of the <see cref="RequestOptions{TCompleated, TFailed}"/> instance
        /// </summary>
        /// <returns>A <see cref="RequestOptions{TCompleated, TFailed}"/> bases on this intance</returns>
        public virtual RequestOptions<TCompleated, TFailed> Clone() => Clone<RequestOptions<TCompleated, TFailed>>();

        /// <summary>
        /// Creates a copy of the <see cref="RequestOptions{TCompleated, TFailed}"/> instance
        /// </summary>
        /// <returns>A <see cref="RequestOptions{TCompleated, TFailed}"/> bases on this intance</returns>
        internal virtual TOptions Clone<TOptions>() where TOptions : RequestOptions<TCompleated, TFailed>, new()
        {
            TOptions options = new()
            {
                Priority = Priority,
                Handler = Handler,
                TryCounter = TryCounter,
                CancellationToken = CancellationToken,
                AutoStart = AutoStart,
                DelayBetweenAttemps = DelayBetweenAttemps,
                DeployDelay = DeployDelay
            };
            options.RequestCancelled += RequestCancelled;
            options.RequestStarted += RequestStarted;
            options.RequestFailed = RequestFailed;
            options.RequestCompleated = RequestCompleated;
            return options;
        }
    }
}
