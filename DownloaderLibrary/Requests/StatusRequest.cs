namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Request that only gets the Headers of an URL.
    /// Send a <see cref="HttpMethod.Head"/> request.
    /// </summary>
    public class StatusRequest : Request
    {
        /// <summary>
        /// Constructor for the <see cref="StatusRequest"/>
        /// CompleatedAction returns a <see cref="HttpResponseMessage"/>
        /// </summary>
        /// <param name="url">URL to get the Head response from</param>
        /// <param name="options">Options to modify the <see cref="Request"/></param>
        public StatusRequest(string url, RequestOptions? options = null) : base(url, options) { if (Options.AutoStart) Start(); }

        /// <summary>
        /// Gets the Head response from the URL that was setted.
        /// </summary>
        /// <returns>A <see cref="Boolean"/> that indicates sucess</returns>
        protected override async Task<bool> RunRequestAsync()
        {
            try
            {
                using CancellationTokenSource? tok = CancellationTokenSource.CreateLinkedTokenSource(Token);
                tok.CancelAfter(TimeSpan.FromSeconds(10));
                using HttpResponseMessage? res = await RequestHandler.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, _url), tok.Token);
                if (res.IsSuccessStatusCode)
                    Options.RequestCompleated?.Invoke(res);
                else Options.RequestFailed?.Invoke(res);
                res.Dispose();
            }
            catch (Exception)
            {
                Options.RequestFailed?.Invoke(new HttpResponseMessage());
                return false;
            }
            return true;

        }
    }
}
