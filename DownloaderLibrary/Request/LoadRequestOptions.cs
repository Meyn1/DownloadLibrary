using DownloaderLibrary.Utilities;

namespace DownloaderLibrary.Request
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

    public class LoadRequestOption : RequestOptions
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
        public LoadRequestOption() { }
        /// <summary>
        /// Constructor that copys the values from one <see cref="LoadRequestOption"/> to a new one.
        /// </summary>
        /// <param name="options">Option that sould be copied</param>
        public LoadRequestOption(LoadRequestOption? options) : base(options)
        {
            if (options == null)
                return;
            IsDownload = options.IsDownload;
            ExcludedExtensions = options.ExcludedExtensions;
            Mode = options.Mode;
            Progress = options.Progress;
            _fileName = options.FileName;
            _temporaryPath = options.TemporaryPath;
            _destinationPath = options.DestinationPath;
        }
    }
}
