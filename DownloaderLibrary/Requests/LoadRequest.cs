using DownloaderLibrary.Utilities;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security;

namespace DownloaderLibrary.Requests
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

        private Range Range => Options.Range;

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
            if (options?.Range.Start >= options?.Range.End)
                throw new IndexOutOfRangeException(nameof(options.Range.End) + " has to be less that " + nameof(options.Range.Start));
            if (options?.Range.Start != null && options?.Mode == LoadMode.Append)
                options.Mode = LoadMode.Create;

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
        /// Instanziates the Lazy <see cref="ContentLength"/>.
        /// Called only one time
        /// </summary>
        /// <returns>A nullable Long that wants to contain the length of the content</returns>
        private long? GetContentLength()
        {
            long? length = null;
            try
            {
                using HttpResponseMessage? responseMessage = RequestHandler.HttpClient.Send(new HttpRequestMessage(HttpMethod.Head, _url), Token);
                if (responseMessage.IsSuccessStatusCode)
                    length = responseMessage.Content.Headers.ContentLength;
                if (Range.Start != null || Range.End != null)
                {
                    if (Range.End > length)
                        Options.Range = new Range(Range.Start, null);
                    if (Range.Length == null)
                        length -= Range.Start;
                    else
                        length = Range.Length;
                }
            }
            catch (ArgumentNullException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            return length == 0 ? null : length;
        }

        /// <summary>
        /// Loads file info if the file exsists
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void LoadFileInfo()
        {

            string fileName = Options.FileName;
            if (Options.Mode != LoadMode.Append || BytesWritten != null || !fileName.Contains('.'))
                return;
            try
            {
                string tmpPath = Path.Combine(Options.TemporaryPath, fileName + ".part");
                if (File.Exists(tmpPath))
                    BytesWritten = new FileInfo(tmpPath).Length;
                else if (Path.Combine(Options.DestinationPath, fileName) is string path && File.Exists(path))
                {
                    BytesWritten = new FileInfo(path).Length;
                    File.Move(path, tmpPath, true);
                }
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
        /// Handels the Downloadfuntion of this <see cref="Request"/>.
        /// </summary>
        /// <returns></returns>
        protected override async Task<bool> RunRequestAsync()
        {
            try
            {
                (bool result, HttpResponseMessage message) result = await Load();
                result.message.Dispose();
                if (result.result)
                {
                    if (State == RequestState.Running)
                        Options.Progress?.Report(1);
                    Options.CompleatedAction?.Invoke(Path.Combine(Options.DestinationPath, Options.FileName));
                }
                else Options.FaultedAction?.Invoke(result.message);
                return result.result;
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

        /// <summary>
        /// Dowloads the Content
        /// </summary>
        /// <returns>A Tupple that contains the Respnonse and a bool that indicates if it was succesful </returns>
        private async Task<(bool, HttpResponseMessage)> Load()
        {
            if (IsFinished())
                return new(true, new());

            Stopwatch stopwatch = new();
            stopwatch.Start();

            HttpResponseMessage res = await SendHttpMenssage();

            Debug.WriteLine(res.StatusCode);
            if (!res.IsSuccessStatusCode)
                return new(false, res);
            bool fileNowLoaded = BytesWritten == null;
            SetFileInfo(res.Content.Headers);
            if (IsFinished())
                return new(true, res);
            else if (BytesWritten > 1048576 && fileNowLoaded)
            {
                HttpResponseMessage? newRes = await SendHttpMenssage();
                if (newRes.IsSuccessStatusCode)
                {
                    res.Dispose();
                    res = newRes;
                }
            }
            long tmpBytesWritten = 0;
            string tmpPath = Path.Combine(Options.TemporaryPath, Options.FileName + ".part");

            if (res.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                File.Create(tmpPath).Dispose();
                BytesWritten = 0;
                _contentLength = new(res.Content.Headers?.ContentLength);
            }
            else
                tmpBytesWritten = BytesWritten ?? 0;

            string path = Path.Combine(Options.DestinationPath, Options.FileName);
            if (!File.Exists(path))
                File.Create(path).Dispose();

            await WriterAsync(res);

            stopwatch.Stop();
            if (State == RequestState.Running)
                File.Move(tmpPath, path, true);
            if (BytesWritten != null)
                Options.RequestHandler.AddSpeed((int)((BytesWritten - tmpBytesWritten) / stopwatch.Elapsed.TotalSeconds));
            return new(true, res);
        }

        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            HttpRequestMessage? msg = new(HttpMethod.Get, _url);
            Range loadRange = new((Range.Start ?? 0) + (BytesWritten ?? 0), Range.End);

            Debug.WriteLine(loadRange.Start);
            Debug.WriteLine(loadRange.End);
            if (loadRange.Length != null || loadRange.Start != 0)
                msg.Headers.Range = new RangeHeaderValue(loadRange.Start, loadRange.End);

            msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");
            return await RequestHandler.HttpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, Token);
        }

        /// <summary>
        /// Creates a file out of the Information that were given
        /// </summary>
        /// <param name="headers">Response from the HttpClient</param>
        private void SetFileInfo(HttpContentHeaders headers)
        {
            if (headers.ContentLength.HasValue)
            {
                long length = headers.ContentLength.Value;
                if ((_contentLength.Value ?? 0) != length &&
               (_contentLength.Value ?? 0) != length + (BytesWritten ?? 0))
                    _contentLength = new(length);
            }

            string fileName = Options.FileName;
            if (fileName == string.Empty)
            {
                fileName = headers.ContentDisposition?.FileNameStar ?? string.Empty;
                if (fileName == string.Empty)
                    fileName = IOManager.RemoveInvalidFileNameChars(new Uri(_url).Segments.Last() ?? string.Empty);
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

            string path = Path.Combine(Options.DestinationPath, fileName);
            string tmpPath = Path.Combine(Options.TemporaryPath, fileName + ".part");
            switch (Options.Mode)
            {
                case LoadMode.Overwrite:
                    if (BytesWritten != null)
                        break;
                    File.Create(path).Dispose();
                    File.Create(tmpPath).Dispose();
                    break;
                case LoadMode.Create:
                    if (!File.Exists(path) && !File.Exists(tmpPath))
                        break;
                    if (BytesWritten != null)
                        break;

                    int index = fileName.LastIndexOf('.');
                    string name = fileName;
                    for (int i = 1; File.Exists(path) || File.Exists(tmpPath); i++)
                    {
                        name = index != -1 ? fileName.Insert(index, $"({i})") : fileName + $"({i})";
                        path = Path.Combine(Options.DestinationPath, name);
                        tmpPath = Path.Combine(Options.TemporaryPath, name + ".part");
                    }
                    fileName = name;
                    File.Create(path).Dispose();
                    File.Create(tmpPath).Dispose();
                    break;
                case LoadMode.Append:
                    Options.FileName = fileName;
                    LoadFileInfo();
                    if (BytesWritten > ContentLength)
                    {
                        File.Delete(path);
                        File.Delete(tmpPath);
                        BytesWritten = null;
                    }
                    break;
            }
            BytesWritten ??= 0;
            Options.FileName = fileName;
        }


        private bool IsFinished()
        {
            if (BytesWritten != 0 && BytesWritten != null && BytesWritten == ContentLength)
            {
                if (File.Exists(Path.Combine(Options.TemporaryPath, Options.FileName + ".part")))
                    File.Move(Path.Combine(Options.TemporaryPath, Options.FileName + ".part"), Path.Combine(Options.DestinationPath, Options.FileName), true);
                return true;
            }
            return false;
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


        private static void CombineMultipleFiles(string inputDirectoryPath, string inputFileNamePattern, string outputFilePath)
        {
            string[] inputFilePaths = Directory.GetFiles(inputDirectoryPath, inputFileNamePattern);
            foreach (string inputFilePath in inputFilePaths)
            {
                using StreamWriter outputStream = File.AppendText(outputFilePath);
                // Buffer size can be passed as the second argument.
                outputStream.WriteLine(File.ReadAllText(inputFilePath));
            }
        }

    }

}
