using DownloaderLibrary.Utilities;

namespace DownloaderLibrary.Requests
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
    /// <summary>
    /// A Class to hold the options for a <see cref="LoadRequest"/> class and to modify it.
    /// </summary>

    public class LoadRequestOptions : RequestOptions
    {
        /// <summary>
        /// File writing mode
        /// </summary>
        public LoadMode Mode { get; set; } = LoadMode.Append;
        /// <summary>
        /// Extensions that sould not be loaded.
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();
        /// <summary>
        /// Progress to watch the load process.
        /// </summary>
        public IProgress<float>? Progress { get; set; } = null;

        /// <summary>
        /// Add Headers to the <see cref="LoadRequest"/> like a useragend
        /// <para>e.g. Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");</para>
        /// </summary>
        public Dictionary<string, string> Headers = new();

        private string _fileName = string.Empty;
        /// <summary>
        /// Filename of the file that will be created and be written to.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            set => _fileName = IOManager.RemoveInvalidFileNameChars(value);
        }

        /// <summary>
        /// Timeout of the <see cref="Request"/>.
        /// </summary>
        public TimeSpan? Timeout { get; set; } = null;

        /// <summary>
        /// Chunks the <see cref="LoadRequest"/> and partial downloads the file
        /// <para>(Only if server supports it)</para>
        /// </summary>
        public byte Chunks { get; set; }

        /// <summary>
        /// Sets the download range of th<see cref="LoadRequest"/> 
        /// Start can not be used with LoadMode.Append it will switch to LoadMode.Create
        /// </summary>
        public Range Range { get; set; }

        /// <summary>
        /// Path to the diriectory where the .part file sould be stored.
        /// Default is the <see cref="DestinationPath"/>.
        /// </summary>
        public string TemporaryPath
        {
            get => _temporaryPath;
            set
            {
                if (value == string.Empty)
                {
                    _temporaryPath = string.Empty;
                    return;
                }
                if (!IOManager.TryGetFullPath(value, out string path))
                    throw new ArgumentException("Path is not valid", nameof(TemporaryPath));

                _temporaryPath = path;
            }

        }
        private string _temporaryPath = string.Empty;
        /// <summary>
        /// Path to where the file sould be stored to.
        /// </summary>
        public string DestinationPath
        {
            get => _destinationPath;
            set
            {
                if (!IOManager.TryGetFullPath(value, out string path))
                    throw new ArgumentException("Path is not valid", nameof(DestinationPath));
                _destinationPath = path;
            }
        }
        private string _destinationPath = IOManager.GetDownloadFolderPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);

        /// <summary>
        /// Default constructor
        /// </summary>
        public LoadRequestOptions() { }
        /// <summary>
        /// Constructor that copys the values from one <see cref="LoadRequestOptions"/> to a new one.
        /// </summary>
        /// <param name="options">Option that sould be copied</param>
        /// <exception cref="ArgumentNullException">Throws exception if <see cref="LoadRequestOptions"/> is null</exception>
        public LoadRequestOptions(LoadRequestOptions options) : base(options)
        {
            ExcludedExtensions = options.ExcludedExtensions;
            Mode = options.Mode;
            Progress = options.Progress;
            Chunks = options.Chunks;
            Range = options.Range;
            Headers = options.Headers;
            _fileName = options.FileName;
            _temporaryPath = options.TemporaryPath;
            _destinationPath = options.DestinationPath;
        }
    }

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
