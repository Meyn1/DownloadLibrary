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

        private Chunk? _chunk;

        private string Destination { get; set; } = string.Empty;
        private string TmpDestination { get; set; } = string.Empty;
        private string TempExt
        {
            get => _tempExt; set
            {
                _tempExt = value;
                TmpDestination = Path.Combine(Options.TemporaryPath, Options.FileName + TempExt);
            }
        }
        private string _tempExt = ".part";


        /// <summary>
        /// Constructor for a <see cref="LoadRequest"/>.
        /// </summary>
        /// <param name="sourceUrl">URL of the content that sould be saved</param>
        /// <param name="options">Options to modify the <see cref="LoadRequest"/></param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
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
                using HttpResponseMessage? res = RequestHandler.HttpClient.Send(new HttpRequestMessage(HttpMethod.Head, _url), Token);
                if (!res.IsSuccessStatusCode)
                    return length;
                length = res.Content.Headers.ContentLength;

                _chunk?.Requests.ForEach(request =>
                {
                    if (request != this)
                        request.SetRange(length);
                });

                length = SetRange(length);
            }
            catch (ArgumentNullException) { }
            catch (InvalidOperationException) { }
            catch (NotSupportedException) { }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            return length == 0 ? null : length;
        }

        /// <summary>
        /// Sets the Range of <see cref="Options"/> to a fitting value for the request
        /// </summary>
        /// <param name="length">legth of the content</param>
        /// <returns>length of the content fitting to the Range</returns>
        private long? SetRange(long? length)
        {
            if (Range.Start != null || Range.End != null)
            {
                if (Range.Length > length)
                    Options.Range = new Range(Range.Start, null);
                if (Range.End == null)
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
            if (Options.Mode != LoadMode.Append || BytesWritten != null || !Options.FileName.Contains('.'))
                return;
            try
            {
                TmpDestination = Path.Combine(Options.TemporaryPath, Options.FileName + TempExt);
                Destination = Path.Combine(Options.DestinationPath, Options.FileName);
                if (File.Exists(TmpDestination))
                    BytesWritten = new FileInfo(TmpDestination).Length;
                else if (File.Exists(Destination))
                {
                    BytesWritten = new FileInfo(Destination).Length;
                    if (_chunk == null)
                        File.Move(Destination, TmpDestination, true);
                }
            }

            catch (ArgumentNullException) { }
            catch (SecurityException) { }
            catch (ArgumentException) { }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (NotSupportedException) { }
            if (Options.ExcludedExtensions.Any(x => Options.FileName.EndsWith(x)))
                throw new InvalidOperationException($"FileName ends with invalid extension");
        }

        private void InitializeChunks()
        {
            if (!(Options.Chunks > 1))
                return;

            IProgress<float>? progress = Options.Progress;
            _chunk = new()
            {
                Requests = new(Options.Chunks) { this },
                Index = 0,
                Destinations = new string[Options.Chunks],
                OnCompleated = Options.CompleatedAction,
                Progress = progress != null ? new float[Options.Chunks] : null,
                MainProgress = progress
            };
            Options.CompleatedAction = null;

            if (_chunk.Progress != null)
                Options.Progress = new Progress<float>(f =>
                {
                    lock (_chunk.Progress)
                    {
                        _chunk.Progress[_chunk.Index] = f;
                        progress?.Report(_chunk.Progress.Average());
                    }
                });

            TempExt = "_0.chunk";
            for (byte i = 1; i < Options.Chunks; i++)
                _chunk.Requests.Add(new LoadRequest(_chunk, this, i));
        }

        private LoadRequest(Chunk chunk, LoadRequest startRequest, byte index) : base(startRequest._url, null)
        {
            _contentLength = startRequest._contentLength;

            base.Options = new LoadRequestOptions(startRequest.Options);

            _chunk = new()
            {
                Requests = chunk.Requests,
                Index = index,
                Destinations = chunk.Destinations,
                Progress = chunk.Progress,
                MainProgress = chunk.MainProgress
            };
            if (_chunk.Progress != null)
                Options.Progress = new Progress<float>(f =>
                {
                    lock (_chunk.Progress)
                    {
                        _chunk.Progress[_chunk.Index] = f;
                        _chunk.MainProgress?.Report(_chunk.Progress.Average());
                    }
                });
            TempExt = $"_{index}.chunk";
            Start();
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
                if (State == RequestState.Running)
                    if (result.result)
                    {
                        Options.Progress?.Report(1);
                        Options.CompleatedAction?.Invoke(Destination);
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
            {
                if (_chunk != null && res.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    Options?.Progress?.Report(1);
                    Copy();
                    return new(true, res);
                }
                else
                    return new(false, res);
            }
            bool fileLoaded = (BytesWritten ?? 0) != 0;
            bool lengthLoaded = ContentLength != null;

            SetFileInfo(res.Content.Headers);

            if (IsFinished() || State != RequestState.Running)
                return new(true, res);

            if (!fileLoaded && BytesWritten > 1048576 || !lengthLoaded && (_chunk != null || Range.Length != null))
            {
                if (!lengthLoaded)
                    _ = SetRange(res.Content.Headers.ContentLength);
                HttpResponseMessage? newRes = await SendHttpMenssage();
                if (newRes.IsSuccessStatusCode)
                {
                    res.Dispose();
                    res = newRes;
                }

            }

            if (res.Content.Headers.ContentLength.HasValue)
            {
                long length = res.Content.Headers.ContentLength.Value;
                if ((_contentLength.Value ?? 0) != length &&
               (_contentLength.Value ?? 0) != length + (BytesWritten ?? 0))
                    _contentLength = new(length);
            }


            long tmpBytesWritten = 0;
            if (res.StatusCode != System.Net.HttpStatusCode.PartialContent)
            {
                if (_chunk != null)
                {
                    for (int i = 0; i < _chunk?.Requests.Count; i++)
                    {
                        LoadRequest req = _chunk.Requests[i];
                        if (i == 0)
                        {
                            req.Options.Progress = _chunk?.MainProgress;
                            req._chunk = null;
                            req.TempExt = ".part";
                        }
                        else if (i != _chunk.Index)
                            req.Cancel(true);

                        if (File.Exists(req.TmpDestination))
                            File.Delete(req.TmpDestination);
                    }

                    if (_chunk != null)
                    {
                        Cancel(true);
                        return new(true, res);
                    }
                }

                File.Create(TmpDestination).Dispose();
                BytesWritten = 0;
            }
            else
                tmpBytesWritten = BytesWritten ?? 0;

            if (!File.Exists(Destination) && (_chunk?.Index == 0 || _chunk == null))
                File.Create(Destination).Dispose();

            await WriterAsync(res);

            stopwatch.Stop();

            Copy();

            if (BytesWritten != null)
                Options.RequestHandler.AddSpeed((int)((BytesWritten - tmpBytesWritten) / stopwatch.Elapsed.TotalSeconds));

            return new(true, res);
        }

        private void Copy()
        {
            if (State == RequestState.Running)
                if (_chunk == null)
                    File.Move(TmpDestination, Destination, true);
                else
                {
                    _chunk.Destinations[_chunk.Index] = TmpDestination;
                    if (Array.TrueForAll(_chunk.Destinations, x => !string.IsNullOrEmpty(x)))
                        CombineMultipleFiles();
                }
        }

        private bool IsFinished()
        {
            if (BytesWritten > 0 && BytesWritten == ContentLength)
            {
                if (_chunk != null)
                    _chunk.Destinations[_chunk.Index] = TmpDestination;
                else if (File.Exists(TmpDestination))
                    File.Move(TmpDestination, Destination, true);
                return true;
            }
            return false;
        }

        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            HttpRequestMessage? msg = new(HttpMethod.Get, _url);

            if (_chunk != null && !_chunk.IsRangeSet)
            {
                long? contentLength = Range.Length == null ? ContentLength - (Range.Start ?? 0) : Range.Length;
                contentLength = contentLength > ContentLength ? ContentLength - (Range.Start ?? 0) : contentLength;
                long? chunkStart = (Range.Start ?? 0) + contentLength / Options.Chunks * _chunk.Index;
                long? chunkEnd = _chunk.Index + 1 == Options.Chunks ? Range.End :
                    (Range.Start ?? 0) + ((_chunk.Index + 1) * (contentLength / Options.Chunks)) - 1;
                Options.Range = new Range(chunkStart, chunkEnd);
                _chunk.IsRangeSet = true;

            }
            Range loadRange = new((Range.Start ?? 0) + (BytesWritten ?? 0), Range.End);
            if (loadRange.Length != null || loadRange.Start != 0)
                msg.Headers.Range = new RangeHeaderValue(loadRange.Start, loadRange.End);

            if (!Options.Headers.ContainsKey("User-Agent"))
                msg.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36");
            foreach (KeyValuePair<string, string> keyValuePair in Options.Headers)
                msg.Headers.Add(keyValuePair.Key, keyValuePair.Value);

            if (State != RequestState.Running)
                return new();
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

            Destination = Path.Combine(Options.DestinationPath, fileName);
            TmpDestination = Path.Combine(Options.TemporaryPath, fileName + TempExt);
            switch (Options.Mode)
            {
                case LoadMode.Overwrite:
                    if (BytesWritten != null)
                        break;
                    File.Create(TmpDestination).Dispose();
                    if (_chunk == null || _chunk.Index == 0)
                        File.Create(Destination).Dispose();
                    break;
                case LoadMode.Create:
                    if ((!File.Exists(Destination) && !File.Exists(TmpDestination)) || BytesWritten != null)
                        break;
                    int index = fileName.LastIndexOf('.');
                    string name = fileName;

                    for (int i = 1; (File.Exists(Destination) && (_chunk?.Index ?? 0) == 0) || File.Exists(TmpDestination); i++)
                    {
                        name = index != -1 ? fileName.Insert(index, $"({i})") : fileName + $"({i})";
                        Destination = Path.Combine(Options.DestinationPath, name);
                        TmpDestination = Path.Combine(Options.TemporaryPath, name + TempExt);
                    }
                    fileName = name;
                    if (_chunk != null || _chunk?.Index == 0)
                        File.Create(Destination).Dispose();
                    File.Create(TmpDestination).Dispose();

                    break;
                case LoadMode.Append:
                    Options.FileName = fileName;
                    LoadFileInfo();
                    if (BytesWritten > ContentLength)
                    {
                        if (_chunk != null)
                        {
                            File.Create(Destination).Dispose();
                            File.Create(TmpDestination).Dispose();
                            BytesWritten = null;
                        }
                        else Cancel();
                    }
                    break;
            }
            BytesWritten ??= 0;
            Options.FileName = fileName;
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
            string startFile = _chunk.Destinations[0];
            File.Move(startFile, _chunk.Requests[0].Destination, true);
            string[] inputFilePaths = _chunk.Destinations[1..];
            using FileStream outputStream = new(_chunk.Requests[0].Destination, FileMode.Append);
            foreach (string inputFilePath in inputFilePaths)
            {
                using FileStream inputStream = File.OpenRead(inputFilePath);
                inputStream.CopyTo(outputStream);
                inputStream.Close();
                File.Delete(inputFilePath);
            }
            for (int i = 0; i < _chunk?.Progress?.Length; i++)
                _chunk.Progress[i] = 1f;
            _chunk!.OnCompleated?.Invoke(_chunk.Requests[0].Destination);
            Options.Progress?.Report(1f);
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
