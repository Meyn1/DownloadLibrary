namespace DownloaderLibrary.Web
{
    /// <summary>
    /// Sets and Gets the download range of a file if supported
    /// </summary>
    public struct Range
    {
        /// <summary>
        /// Creates a Range out two longs
        /// </summary>
        public Range(long? start, long? end)
        {
            Start = start;
            End = end == 0 ? null : end;
        }

        /// <summary>
        /// Retuns the Length
        /// </summary>
        public long? Length => 1 + End - (Start ?? 0);

        /// <summary>
        /// Start point in bytes
        /// zero based
        /// </summary>
        public long? Start { get; set; } = null;
        /// <summary>
        /// End point in bytes
        /// zero based
        /// </summary>
        public long? End { get; set; } = null;
    }
}
