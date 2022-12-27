using DownloaderLibrary.Web.Request;

namespace DownloaderLibrary.Web
{
    /// <summary>
    /// File load mode of the <see cref="LoadRequest"/>.
    /// </summary>
    public enum LoadMode
    {
        /// <summary>
        /// overwrites a file if it already exists or creates a new one.
        /// </summary>
        Overwrite,
        /// <summary>
        /// Creates always a new file and wiites into that.
        /// </summary>
        Create,
        /// <summary>
        /// Append a already existing file or creates a new one .
        /// </summary>
        Append
    }
}
