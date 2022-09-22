namespace DownloaderLibrary.Request
{
    /// <summary>
    /// A Class to hold the options on a <see cref="Request"/> class and to modify it.
    /// </summary>
    public class RequestOptions
    {
        /// <summary>
        /// If the <see cref="Request"/> has priority over other not prioritized <see cref="Request">Requests</see>.
        /// </summary>
        public bool HasPriority { get; init; } = false;
        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second  <see cref="Thread"/>.
        /// </summary>
        public bool IsDownload { get; init; } = false;
        /// <summary>
        /// How often the <see cref="Request"/> sould be retried if it fails.
        /// </summary>
        public byte TryCounter { get; init; } = 3;
        /// <summary>
        /// <see cref="System.Threading.CancellationToken"/> that the user sets to cancel the <see cref="Request"/>.
        /// </summary>
        public CancellationToken? CancellationToken { get; init; } = null;
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> finished.
        /// </summary>
        public Action<object?>? CompleatedAction { get; init; }
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> failed.
        /// </summary>
        public Action<HttpResponseMessage>? FaultedAction { get; init; }
        /// <summary>
        /// Action that will be called when the <see cref="Request"/> is cancelled.
        /// </summary>
        public Action? CancelledAction { get; init; }

        /// <summary>
        /// Main Constructor for a <see cref="RequestOptions"/> object.
        /// </summary>
        public RequestOptions() { }

        /// <summary>
        /// Creates a <see cref="RequestOptions"/> object baes on another <see cref="RequestOptions"/> object.
        /// </summary>
        /// <param name="options"><see cref="RequestOptions"/> parameter to copy.</param>
        public RequestOptions(RequestOptions? options)
        {
            if (options == null)
                return;
            HasPriority = options.HasPriority;
            IsDownload = options.IsDownload;
            TryCounter = options.TryCounter;
            CancellationToken = options.CancellationToken;
            CompleatedAction = (Action<object?>?)options.CompleatedAction?.Clone();
            FaultedAction = (Action<HttpResponseMessage>?)options.FaultedAction?.Clone();
            CancelledAction = (Action?)options.CancelledAction?.Clone();
        }
    }
}
