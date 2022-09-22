using DownloaderLibrary.Utilities;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security;

namespace DownloaderLibrary.Request
{
    /// <summary>
    /// A <see cref="Request"/> that loads the response and saves it to an file
    /// </summary>
    public class LoadRequest : Request
    {
        private Lazy<long?> _contentLength;
        internal new LoadRequestOption Options => (LoadRequestOption)base.Options;
        /// <summary>
        /// Bytes that were written to the disc
        /// </summary>
        public long? BytesWritten { get; private set; }
        /// <summary>
        /// Length of the content that will be downloaded
        /// </summary>
        public long? ContentLength => _contentLength.Value;

        /// <summary>
        /// Constructor for a <see cref="LoadRequest"/>.
        /// </summary>
        /// <param name="sourceUrl">URL of the content that sould be saved</param>
        /// <param name="options">Options to modify the <see cref="LoadRequest"/></param>
        /// <exception cref="ArgumentNullException"></exception>
        public LoadRequest(string sourceUrl, LoadRequestOption? options = null) : base(sourceUrl, null)
        {
            if (string.IsNullOrEmpty(sourceUrl))
                throw new ArgumentNullException(nameof(sourceUrl));

            base.Options = options != null ? new LoadRequestOption(options) : new LoadRequestOption();
            _contentLength = new Lazy<long?>(GetContentLength);
            Directory.CreateDirectory(Options.DestinationPath);
            if (!string.IsNullOrWhiteSpace(Options.TemporaryPath))
                Directory.CreateDirectory(Options.TemporaryPath);
            else
                Options.TemporaryPath = Options.DestinationPath;
            LoadFileInfo();
            Start();
        }

        /// <summary>
        /// Creates a file out of the Information that were given
        /// </summary>
        /// <param name="headers">Response from the HttpClient</param>
        private void SetFileInfo(HttpContentHeaders headers)
        {
            if (_contentLength.Value != headers.ContentLength && headers.ContentLength.HasValue)
                _contentLength = new Lazy<long?>(headers.ContentLength);

            string fileName = Options.FileName;
            if (fileName == string.Empty)
            {
                fileName = headers.ContentDisposition?.FileNameStar ?? string.Empty;
                if (fileName == string.Empty)
                    fileName = IOManager.RemoveInvalidFileNameChars(Path.GetFileName(_url) ?? string.Empty);
                if (fileName == string.Empty)
                    fileName = "download";
            }

            if (!fileName.Contains('.'))
            {
                string ext = string.Empty;
                if (headers.ContentType?.MediaType != null)
                    ext = IOManager.GetDefaultExtension(headers.ContentType.MediaType);
                if (ext == string.Empty)
                    ext = Path.GetExtension(_url);
                fileName += ext;
            }
            Options.FileName = fileName;
            string path = Path.Combine(Options.DestinationPath, fileName);
            string tmpPath = Path.Combine(Options.TemporaryPath, fileName + ".part");
            switch (Options.Mode)
            {
                case LoadMode.Overwrite:
                    if (File.Exists(path))
                        File.Delete(path);
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                    break;
                case LoadMode.Create:
                    if (!File.Exists(path) && !File.Exists(tmpPath))
                        break;
                    int i = 0;
                    int index = fileName.LastIndexOf('.');
                    while (File.Exists(path) || File.Exists(tmpPath))
                    {
                        fileName = fileName.Insert(index, $"({++i})");
                        path = Path.Combine(Options.DestinationPath, fileName);
                        tmpPath = Path.Combine(Options.TemporaryPath, fileName + ".part");
                    }
                    Options.FileName = fileName;
                    File.Create(path).Dispose();
                    File.Create(tmpPath).Dispose();
                    break;
                case LoadMode.Append:
                    LoadFileInfo();
                    BytesWritten ??= 0;
                    if (BytesWritten > ContentLength)
                    {
                        File.Delete(path);
                        File.Delete(tmpPath);
                        BytesWritten = 0;
                    }
                    break;
            }
        }

        /// <summary>
        /// Loads file info if the file exsists
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void LoadFileInfo()
        {
            string fileName = Options.FileName;
            if (Options.Mode != LoadMode.Append || BytesWritten != null || !fileName.Contains('.') || fileName.EndsWith('.'))
                return;

            try
            {
                if (Path.Combine(Options.TemporaryPath, fileName + ".part") is string tmpPath && File.Exists(tmpPath))
                    BytesWritten = new FileInfo(tmpPath).Length;
                else if (Path.Combine(Options.DestinationPath, fileName) is string path && File.Exists(path))
                    BytesWritten = new FileInfo(path).Length;
            }
            catch (ArgumentNullException) { }
            catch (SecurityException) { }
            catch (ArgumentException) { }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (NotSupportedException) { }
            if (Options.ExcludedExtensions.Any(x => fileName.EndsWith(x)))
                throw new InvalidOperationException($"FileName ends with invalid extension");
        }

        /// <summary>
        /// Instanziates the Lazy <see cref="ContentLength"/>.
        /// </summary>
        /// <returns>A nullable Long that wants to Contain the Length of the Content</returns>
        private long? GetContentLength()
        {
            long? length = null;
            try
            {
                using HttpResponseMessage? responseMessage = DownloadHandler.Instance.Send(new HttpRequestMessage(HttpMethod.Head, _url), Token);
                if (responseMessage.IsSuccessStatusCode)
                    length = responseMessage.Content.Headers.ContentLength;
            }
            catch (ArgumentNullException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            return length == 0 ? null : length;
        }

        /// <summary>
        /// Handels the Downloadfuntion of this <see cref="Request"/>.
        /// </summary>
        /// <returns></returns>
        protected override async Task<bool> RunRequestAsync()
        {
            try
            {
                Tuple<bool, HttpResponseMessage> result = await Load();
                result.Item2.Dispose();
                if (result.Item1)
                {
                    Options.Progress?.Report(1);
                    Options.CompleatedAction?.Invoke(Path.Combine(Options.DestinationPath, Options.FileName));
                }
                else Options.FaultedAction?.Invoke(result.Item2);
                return result.Item1;
            }
            catch (TaskCanceledException) { }
            catch (ArgumentNullException) { }
            catch (ArgumentOutOfRangeException) { }
            catch (InvalidOperationException) { }
            catch (HttpRequestException) { }
            catch (IOException) { }
            catch (Exception) { }
            return false;
        }

        private bool IsFinished()
        {
            if (BytesWritten != 0 && BytesWritten == ContentLength && BytesWritten != null)
            {
                if (File.Exists(Path.Combine(Options.TemporaryPath, Options.FileName + ".part")))
                    File.Move(Path.Combine(Options.TemporaryPath, Options.FileName + ".part"), Path.Combine(Options.DestinationPath, Options.FileName), true);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Dowloads the Content
        /// </summary>
        /// <returns>A Tupple that contains the Respnonse and a bool that indicates if it was succesful </returns>
        private async Task<Tuple<bool, HttpResponseMessage>> Load()
        {
            if (IsFinished())
                return new(true, new());


            Stopwatch stopwatch = new();
            stopwatch.Start();
            HttpRequestMessage? msg = new(HttpMethod.Get, _url);
            if (BytesWritten != null)
                msg.Headers.Range = new RangeHeaderValue(BytesWritten, null);
            msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");

            HttpResponseMessage res = await DownloadHandler.Instance.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, Token);

            if (!res.IsSuccessStatusCode)
                return new(false, res);
            SetFileInfo(res.Content.Headers);
            if (IsFinished())
                return new(true, res);
            else if (BytesWritten > 1048576 && BytesWritten != null)
            {
                msg = new(HttpMethod.Get, _url);
                msg.Headers.Range = new RangeHeaderValue(BytesWritten, null);
                HttpResponseMessage? newRes = await DownloadHandler.Instance.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, Token);
                if (newRes.IsSuccessStatusCode)
                {
                    res.Dispose();
                    res = newRes;
                }
            }
            long tmpBytesWritten = 0;
            string path = Path.Combine(Options.TemporaryPath, Options.FileName);
            if (res.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                File.Create(path + ".part").Dispose();
                BytesWritten = 0;
            }
            else
                tmpBytesWritten = BytesWritten ?? 0;

            if (!File.Exists(path))
                File.Create(path).Dispose();

            await WriterAsync(res);
            stopwatch.Stop();
            File.Move(path + ".part", Path.Combine(Options.DestinationPath, Options.FileName), true);
            if (BytesWritten != null)
                DownloadHandler.Instance.AddSpeed((int)((BytesWritten - tmpBytesWritten) / stopwatch.Elapsed.TotalSeconds));
            return new(true, res);
        }

        /// <summary>
        /// Writes the response to a file
        /// </summary>
        /// <param name="res">Response of <see cref="HttpClient"/></param>
        /// <returns>A awaitable Task</returns>
        private async Task WriterAsync(HttpResponseMessage res)
        {
            using Stream? responseStream = await res.Content.ReadAsStreamAsync(Token);
            using FileStream? fs = new(Path.Combine(Options.TemporaryPath, Options.FileName + ".part"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            IProgress<float>? progress = ContentLength != null ? Options.Progress : null;
            while (State == RequestState.Running)
            {
                byte[]? buffer = new byte[1024];
                int bytesRead = await responseStream.ReadAsync(buffer, Token).ConfigureAwait(false);

                if (bytesRead == 0) break;
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), Token);
                BytesWritten += bytesRead;
                progress?.Report((float)BytesWritten!.Value / (ContentLength!.Value + 10));
            }
            await fs.FlushAsync();
        }


    }
}
