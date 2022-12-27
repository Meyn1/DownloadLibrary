using DownloaderLibrary.Utilities;
using DownloaderLibrary.Web.Request;

namespace DownloaderLibrary.Web.Options
{
    /// <summary>
    /// A Class to hold the options for a <see cref="LoadRequest"/> class and to modify it.
    /// </summary>
    public class LoadRequestOptions : WebRequestOptions<string>
    {
        /// <summary>
        /// Filename of the file that will be created and be written to.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            set => _fileName = IOManager.RemoveInvalidFileNameChars(value);
        }
        private string _fileName = string.Empty;

        /// <summary>
        /// Sets the download range of th<see cref="LoadRequest"/> 
        /// Start can not be used with LoadMode.Append it will switch to LoadMode.Create
        /// </summary>
        public Range Range { get; set; }

        /// <summary>
        /// Extensions that sould not be loaded.
        /// </summary>
        public string[] ExcludedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// File writing mode
        /// </summary>
        public LoadMode Mode { get; set; } = LoadMode.Append;

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
        /// Progress to watch the <see cref="LoadRequest"/>.
        /// </summary>
        public IProgress<float>? Progress { get; set; } = null;

        /// <summary>
        /// Chunks the <see cref="LoadRequest"/> and partial downloads the file
        /// <para>(Only if server supports it)</para>
        /// </summary>
        public byte Chunks { get; set; }

        /// <summary>
        /// Merges the chunked files on the fly and not at the end.
        /// </summary>
        public bool MergeWhileProgress { get; set; }

        /// <summary>
        /// Creates a copy of the <see cref="LoadRequestOptions"/> instance
        /// </summary>
        /// <returns>A <see cref="LoadRequestOptions"/> bases on this intance</returns>
        public override LoadRequestOptions Clone() => Clone<LoadRequestOptions>();

        /// <summary>
        /// Creates a copy of the <see cref="LoadRequestOptions"/> instance
        /// </summary>
        /// <returns>A <see cref="LoadRequestOptions"/> bases on this intance</returns>
        internal override TOptions Clone<TOptions>()
        {
            TOptions options = base.Clone<TOptions>();
            if (options is LoadRequestOptions proved)
            {
                proved.MergeWhileProgress = MergeWhileProgress;
                proved.Progress = Progress;
                proved.Chunks = Chunks;
                proved.Range = Range;
                proved._temporaryPath = TemporaryPath;
                proved._destinationPath = DestinationPath;
                proved.Mode = Mode;
                proved.ExcludedExtensions = ExcludedExtensions;
                proved.FileName = FileName;
            }
            return options;
        }
    }
}
