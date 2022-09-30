namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// A Class to easy implement a <see cref="Request"/> functionality without creating a new <see cref="Request"/> child.
    /// </summary>
    public class OwnRequest : Request
    {
        private readonly Func<CancellationToken, Task<bool>> _own;

        /// <summary>
        /// Constructor to create a <see cref="OwnRequest"/>.
        /// </summary>
        /// <param name="own">Function that contains a request</param>
        /// <param name="requestOptions">Options to modify the <see cref="Request"/></param>
        public OwnRequest(Func<CancellationToken, Task<bool>> own, RequestOptions? requestOptions = null) : base(string.Empty, requestOptions)
        {
            _own = own;
            Start();
        }

        /// <summary>
        /// Handles the <see cref="Request"/> that the <see cref="HttpClient"/> sould start.
        /// </summary>
        /// <returns>A bool that indicates if the <see cref="Request"/> was succesful.</returns>
        protected override async Task<bool> RunRequestAsync()
        {
            bool result = await _own.Invoke(Token);
            if (result)
                Options.CompleatedAction?.Invoke(null);
            else
                Options.FaultedAction?.Invoke(new HttpResponseMessage());
            return result;
        }
    }
}
