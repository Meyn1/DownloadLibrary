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
        internal new LoadRequestOptions Options => (LoadRequestOptions)base.Options;
        /// <summary>
        /// Bytes that were written to the disc
        /// </summary>
        public long? BytesWritten { get; private set; }
        /// <summary>
        /// Length of the content that will be downloaded
        /// </summary>
        public long? ContentLength => _contentLength.Value;

        private Range Range => Options.Range;

        internal Chunk? _chunk;

        private string TempExt { get; set; } = ".part";

        /// <summary>
        /// Constructor for a <see cref="LoadRequest"/>.
        /// </summary>
        /// <param name="sourceUrl">URL of the content that sould be saved</param>
        /// <param name="options">Options to modify the <see cref="LoadRequest"/></param>
        /// <exception cref="ArgumentNullException"></exception>
        public LoadRequest(string sourceUrl, LoadRequestOptions? options = null) : base(sourceUrl, null)
        {
            if (string.IsNullOrEmpty(sourceUrl))
                throw new ArgumentNullException(nameof(sourceUrl));
            if (options?.Range.Start >= options?.Range.End)
                throw new IndexOutOfRangeException(nameof(options.Range.End) + " has to be less that " + nameof(options.Range.Start));
            if (options?.Range.Start != null && options?.Mode == LoadMode.Append)
                options.Mode = LoadMode.Create;

            base.Options = options != null ? new LoadRequestOptions(options) : new LoadRequestOptions();
            _contentLength = new Lazy<long?>(GetContentLength);
            Directory.CreateDirectory(Options.DestinationPath);
            if (!string.IsNullOrWhiteSpace(Options.TemporaryPath))
                Directory.CreateDirectory(Options.TemporaryPath);
            else
                Options.TemporaryPath = Options.DestinationPath;
            LoadFileInfo();
            Start();
            InitializeChunks();
        }

        private void InitializeChunks()
        {
            if (!(Options.Chunks > 0))
                return;
            _chunk = new() { Requests = new() { this }, Index = 0, Destinations = new(Options.Chunks), ProgressList = new(new float[Options.Chunks]) };
            IProgress<float>? progress = Options.Progress;
            Options.Progress = new Progress<float>(f =>
            {
                lock (_chunk.ProgressList)
                { _chunk.ProgressList[_chunk.Index] = f; progress?.Report(_chunk.ProgressList.Sum() / Options.Chunks); }
            });
            TempExt = "_0.chunk";
            for (byte i = 1; i < Options.Chunks; i++)
                _ = new LoadRequest(_chunk, this, i, progress);


        }

        private LoadRequest(Chunk chunk, LoadRequest startRequest, byte index, IProgress<float>? progress) : base(startRequest._url, null)
        {
            _contentLength = startRequest._contentLength;
            base.Options = new LoadRequestOptions(startRequest.Options);

            chunk.Requests.Add(this);
            _chunk = new() { Requests = chunk.Requests, Index = index, Destinations = chunk.Destinations, ProgressList = chunk.ProgressList };
            Options.Progress = new Progress<float>(f =>
            {
                lock (_chunk.ProgressList)
                { _chunk.ProgressList[_chunk.Index] = f; progress?.Report(_chunk.ProgressList.Sum() / Options.Chunks); }
            });
            TempExt = $"_{index}.chunk";
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
                if (_chunk != null)
                    foreach (LoadRequest request in _chunk.Requests)
                    {
                        if (request != this)
                            request.SetRange(length);
                    };
                length = SetRange(length);
            }
            catch (ArgumentNullException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            return length == 0 ? null : length;
        }

        private long? SetRange(long? length)
        {

            if (Range.Start != null || Range.End != null)
            {
                if (Range.End > length)
                    Options.Range = new Range(Range.Start, null);
                if (Range.Length == null)
                    length -= Range.Start;
                else
                    length = Range.Length;
            }
            return length;

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
                string tmpPath = Path.Combine(Options.TemporaryPath, fileName + TempExt);
                if (File.Exists(tmpPath))
                    BytesWritten = new FileInfo(tmpPath).Length;
                else if (_chunk == null && Path.Combine(Options.DestinationPath, fileName) is string path && File.Exists(path))
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

            if (!res.IsSuccessStatusCode)
                return new(false, res);
            bool fileNowLoaded = (BytesWritten ?? 0) == 0;
            bool ContentlegthNowLoaded = (ContentLength ?? 0) == 0;
            SetFileInfo(res.Content.Headers);
            if (IsFinished())
                return new(true, res);
            else if (fileNowLoaded && BytesWritten > 1048576 || ContentlegthNowLoaded && (_chunk != null || Range.Length != null))
            {
                _ = SetRange(res.Content.Headers.ContentLength);
                HttpResponseMessage? newRes = await SendHttpMenssage();
                if (newRes.IsSuccessStatusCode)
                {
                    res.Dispose();
                    res = newRes;
                }
            }
            long tmpBytesWritten = 0;
            string tmpPath = Path.Combine(Options.TemporaryPath, Options.FileName + TempExt);

            if (res.Content.Headers.ContentLength.HasValue)
            {
                long length = res.Content.Headers.ContentLength.Value;
                if ((_contentLength.Value ?? 0) != length &&
               (_contentLength.Value ?? 0) != length + (BytesWritten ?? 0))
                    _contentLength = new(length);
            }
            if (res.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                if (_chunk != null)
                {
                    List<LoadRequest> requests = _chunk.Requests.ToList();
                    for (int i = 1; i < requests.Count; i++)
                    {
                        requests[i].Cancel();
                        string rPath = Path.Combine(Options.TemporaryPath, Options.FileName + requests[i].TempExt);
                        if (File.Exists(rPath))
                            File.Delete(rPath);

                    }
                    if (File.Exists(tmpPath))
                        File.Delete(tmpPath);
                    TempExt = ".part";
                    _chunk = null;
                    tmpPath = Path.Combine(Options.TemporaryPath, Options.FileName + TempExt);
                }
                File.Create(tmpPath).Dispose();
                BytesWritten = 0;
            }
            else
                tmpBytesWritten = BytesWritten ?? 0;


            string path = Path.Combine(Options.DestinationPath, Options.FileName);
            if (!File.Exists(path) && (_chunk?.Index == 0 || _chunk == null))
                File.Create(path).Dispose();

            await WriterAsync(res);

            stopwatch.Stop();

            if (State == RequestState.Running && _chunk == null)
                File.Move(tmpPath, path, true);

            if (BytesWritten != null)
                Options.RequestHandler.AddSpeed((int)((BytesWritten - tmpBytesWritten) / stopwatch.Elapsed.TotalSeconds));
            if (State == RequestState.Running)
                _chunk?.Destinations.Enqueue(tmpPath, _chunk.Index);
            if (_chunk != null && _chunk.Destinations.Count == Options.Chunks)
                CombineMultipleFiles();
            return new(true, res);
        }

        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            HttpRequestMessage? msg = new(HttpMethod.Get, _url);
            Range loadRange = new((Range.Start ?? 0) + (BytesWritten ?? 0), Range.End);

            if (_chunk == null)
            {
                if (loadRange.Length != null || loadRange.Start != 0)
                    msg.Headers.Range = new RangeHeaderValue(loadRange.Start, loadRange.End);
            }
            else
            {
                long? contentLenth;
                if (loadRange.Length == null)
                    contentLenth = ContentLength - (Range.Start ?? 0);
                else
                    contentLenth = loadRange.Length;
                long? chunkStart = (Range.Start ?? 0) + contentLenth / Options.Chunks * _chunk.Index;
                long? chunkEnd = (Range.Start ?? 0) + ((_chunk.Index + 1) * (contentLenth / Options.Chunks)) - 1;
                chunkEnd = _chunk.Index + 1 == Options.Chunks ? loadRange.End : chunkEnd;
                if (chunkStart != null || chunkEnd != null)
                    msg.Headers.Range = new RangeHeaderValue(chunkStart + BytesWritten ?? 0, chunkEnd);
            }

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
            string tmpPath = Path.Combine(Options.TemporaryPath, fileName + TempExt);
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
                        tmpPath = Path.Combine(Options.TemporaryPath, name + TempExt);
                    }
                    fileName = name;
                    if (_chunk == null)
                        File.Create(path).Dispose();
                    File.Create(tmpPath).Dispose();
                    break;
                case LoadMode.Append:
                    Options.FileName = fileName;
                    LoadFileInfo();
                    if (BytesWritten > ContentLength)
                    {
                        File.Create(path).Dispose();
                        File.Delete(tmpPath);
                        BytesWritten = null;
                    }
                    if (_chunk?.Index == 0)
                        File.Create(path).Dispose();
                    break;
            }
            BytesWritten ??= 0;
            Options.FileName = fileName;
        }


        private bool IsFinished()
        {
            if (BytesWritten > 0 && BytesWritten == ContentLength)
            {
                if (_chunk == null && File.Exists(Path.Combine(Options.TemporaryPath, Options.FileName + TempExt)))
                    File.Move(Path.Combine(Options.TemporaryPath, Options.FileName + TempExt), Path.Combine(Options.DestinationPath, Options.FileName), true);
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
            using FileStream? fs = new(Path.Combine(Options.TemporaryPath, Options.FileName + TempExt), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
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

        private void CombineMultipleFiles()
        {
            if (_chunk == null)
                return;
            using FileStream destinationStream = new(Path.Combine(Options.DestinationPath, Options.FileName), FileMode.Append);
            while (_chunk.Destinations.TryDequeue(out string? path, out byte i))
            {
                byte[] tempFileBytes = File.ReadAllBytes(path);
                destinationStream.Write(tempFileBytes, 0, tempFileBytes.Length);
                File.Delete(path);
            }
        }


        /// <summary>
        /// Start the <see cref="Request"/> if it is not yet started or paused.
        /// </summary>
        public void Start(bool isOnly = false)
        {
            base.Start();
            if (!isOnly)
                _chunk?.Requests.ForEach(x => x.Start(true));
        }
        /// <summary>
        /// Set the <see cref="Request"/> on hold.
        /// </summary>
        public void Pause(bool isOnly = false)
        {
            base.Pause();
            if (!isOnly)
                _chunk?.Requests.ForEach(x => x.Pause(true));
        }

        /// <summary>
        /// Cancel the <see cref="Request"/>
        /// </summary>
        /// /// <exception cref="AggregateException"></exception>
        /// /// <exception cref="ObjectDisposedException"></exception>
        /// /// <exception cref="InvalidOperationException"></exception>
        public void Cancel(bool isOnly = false)
        {
            base.Cancel();
            if (!isOnly)
                _chunk?.Requests.ForEach(x => x.Cancel(true));
        }

        /// <summary>
        /// Wait to finish this <see cref="Request"/>.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Wait(bool isOnly = false)
        {
            base.Wait();
            if (!isOnly)
                _chunk?.Requests.ForEach(x => x.Wait(true));
        }



    }


}
