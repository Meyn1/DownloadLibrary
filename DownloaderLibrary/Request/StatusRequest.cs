﻿namespace DownloaderLibrary.Request
{
    /// <summary>
    /// Request that only gets the Headers of an URL.
    /// Send a <see cref="HttpMethod.Head"/> request.
    /// </summary>
    public class StatusRequest : Request
    {
        /// <summary>
        /// Constructor for the <see cref="StatusRequest"/>
        /// </summary>
        /// <param name="url">URL to get the Head response from</param>
        /// <param name="options">Options to modify the <see cref="Request"/></param>
        public StatusRequest(string url, RequestOptions? options = null) : base(url, options) { Start(); }

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
                using HttpResponseMessage? res = await Utilities.DownloadHandler.Instance.SendAsync(new HttpRequestMessage(HttpMethod.Head, _url), tok.Token);
                if (res.IsSuccessStatusCode)
                    Options.CompleatedAction?.Invoke(res);
                else Options.FaultedAction?.Invoke(res);
                res.Dispose();
            }
            catch (Exception)
            {
                Options.FaultedAction?.Invoke(new HttpResponseMessage());
                return false;
            }
            return true;

        }
    }
}
