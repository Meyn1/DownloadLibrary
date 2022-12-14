using DownloaderLibrary.Web.Options;
using DownloaderLibrary.Web.Request;

namespace DownloaderLibrary.Web
{
    /// <summary>
    /// Web item with Information of the file
    /// </summary>
    public record WebItem
    {
        /// <summary>
        /// Constructor of WebItem
        /// </summary>
        /// <param name="url">The URL of the Item</param>
        /// <param name="title">Title of the Item</param>
        /// <param name="description">Description of the Item</param>
        /// <param name="typeRaw">Raw Media type of the Item</param>
        public WebItem(Uri url, string title, string description, string typeRaw)
        {
            URL = url;
            Description = description;
            Title = title;
            Type = new(typeRaw);
        }
        /// <summary>
        /// Description of the WebItem
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Uri that holds Url of Webitem
        /// </summary>
        public Uri URL { get; set; }
        /// <summary>
        /// Title of the WebItem
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Type of the WebItem
        /// </summary>
        public WebType Type { get; set; }
        /// <summary>
        /// Creates a <see cref="LoadRequest"/> out of this WebItem
        /// </summary>
        /// <param name="requestOptions">Options for the Request</param>
        /// <returns>Returns a new <see cref="LoadRequest"/></returns>

        public LoadRequest CreateLoadRequest(Web.Options.LoadRequestOptions? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

        /// <summary>
        /// Creates a <see cref="StatusRequest"/> to see if the file is available.
        /// </summary>
        /// <param name="requestOptions">Options for the Request</param>
        /// <returns>A <see cref="StatusRequest"/></returns>
        public StatusRequest CreateStatusRequest(WebRequestOptions<HttpResponseMessage>? requestOptions = null) => new(URL.AbsoluteUri, requestOptions);

    }
}
