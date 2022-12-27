using DownloaderLibrary.Base.Request;
using System.Net;

namespace DownloaderLibrary.Web.Options
{
    /// <summary>
    /// An implementation of an IWebRequestOptions as generic
    /// </summary>
    /// <typeparam name="TCompleated">Type of return if compleated</typeparam>
    public class WebRequestOptions<TCompleated> : RequestOptions<TCompleated, HttpResponseMessage?>, IWebRequestOptions<TCompleated>
    {
        /// <summary>
        /// If the <see cref="Request"/> is an big file and sold download in a second <see cref="Thread"/>.
        /// Can't be used if <see cref="RequestHandler"/> is manually set.
        /// </summary>
        public bool IsDownload
        {
            get => _isDownload; set
            {
                _isDownload = value;
                if (value && Handler == RequestHandler.MainRequestHandlers[0])
                    Handler = RequestHandler.MainRequestHandlers[1];
                else if (!value && Handler == RequestHandler.MainRequestHandlers[1])
                    Handler = RequestHandler.MainRequestHandlers[0];
            }
        }
        private bool _isDownload = false;

        ///<inheritdoc />
        public string UserAgent { get; set; } = string.Empty;
        ///<inheritdoc />
        public WebHeaderCollection Headers { get; } = new();
        ///<inheritdoc />
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Creates a copy of the <see cref="WebRequestOptions{TDelegateCompleated}"/> instance
        /// </summary>
        /// <returns>A <see cref="WebRequestOptions{TDelegateCompleated}"/> bases on this intance</returns>
        public override WebRequestOptions<TCompleated> Clone() => Clone<WebRequestOptions<TCompleated>>();

        /// <summary>
        /// Creates a copy of the <see cref="WebRequestOptions{TDelegateCompleated}"/> instance
        /// </summary>
        /// <returns>A <see cref="WebRequestOptions{TDelegateCompleated}"/> bases on this intance</returns>
        internal override TOptions Clone<TOptions>()
        {
            TOptions options = base.Clone<TOptions>();
            if (options is WebRequestOptions<TCompleated> proved)
            {
                proved.IsDownload = IsDownload;
                proved.Timeout = Timeout;
                foreach (string key in Headers.AllKeys)
                    proved.Headers.Add(key, Headers[key]);
                proved.UserAgent = UserAgent;
            }
            return options;
        }
    }
}
