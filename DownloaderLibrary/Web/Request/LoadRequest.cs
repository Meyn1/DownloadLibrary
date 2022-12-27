using DownloaderLibrary.Base.Request;
using DownloaderLibrary.Utilities;
using DownloaderLibrary.Web.Options;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace DownloaderLibrary.Web.Request
{
    /// <summary>
    /// A <see cref="WebRequest{TOptions, TCompleated}"/> that loads the response as stream and saves it to a file
    /// </summary>
    public class LoadRequest : WebRequest<LoadRequestOptions, string>
    {
        /// <summary>
        /// Min Byte legth to restart the request and download only partial
        /// </summary>
        private const int MIN_RELOAD = 1048576;

        /// <summary>
        /// Lazy loding content legth of the downlod file 
        /// </summary>
        private Lazy<long?> _contentLength;
        /// <summary>
        /// Bytes that were written to the destination file
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <summary>
        /// Length of the content that will be downloaded
        /// </summary>
        public long? ContentLength => _contentLength.Value;

        /// <summary>
        /// Range that should be downloaded
        /// </summary>
        private Range Range => Options.Range;

        /// <summary>
        /// Holds information of the chunked file download process
        /// </summary>
        private ChunkInfo<string>? ChunkInfo { get; set; } = null;

        /// <summary>
        /// Indicates if the <see cref="LoadRequest"/> is a chunked request and downloads parts of the file
        /// </summary>
        public bool IsChunked => ChunkInfo != null;

        /// <summary>
        /// Index for the <see cref="ChunkInfo{TNotify}.Chunks"/>
        /// </summary>
        private readonly byte _chunkIndex;

        /// <summary>
        /// Boolean that indicates if the Request should be continued (null)
        /// </summary>
        private bool? _success;

        /// <summary>
        /// Path to the download file
        /// </summary>
        internal string Destination { get; private set; } = string.Empty;

        /// <summary>
        /// Path to the temporary created file
        /// </summary>
        internal string TmpDestination { get; private set; } = string.Empty;
        /// <summary>
        /// Gets the extension of the temporary created file
        /// </summary>
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
        /// A <see cref="CancellationTokenSource"/> that will be used to let <see cref="WebRequestOptions{TCompleated}.Timeout"/> run.
        /// </summary>
        private CancellationTokenSource? _timeoutCTS;

        /// <summary>
        /// Constructor for a <see cref="LoadRequest"/>.
        /// </summary>
        /// <param name="sourceUrl">URL of the content that sould be saved</param>
        /// <param name="options">Options to modify the <see cref="LoadRequest"/></param>
        /// <exception cref="ArgumentNullException">Throws if sourceUrl is null</exception>
        /// <exception cref="IndexOutOfRangeException">Throws if <see cref="LoadMode.Append"/> is used with <see cref="Range.Start"/></exception>
        public LoadRequest(string sourceUrl, LoadRequestOptions? options = null) : base(sourceUrl, options)
        {
            if (Range.Start >= Range.End)
                throw new IndexOutOfRangeException(nameof(Range.Start) + " has to be less that " + nameof(Range.End));
            if (Range.Start != null && Options.Mode == LoadMode.Append)
                throw new NotSupportedException($"Can not set {nameof(LoadMode.Append)} if {nameof(Range.Start)} was set");

            _contentLength = new(GetContentLength);
            CreateFiles();
            LoadBytesWritten();
            AutoStart();
            InitializeChunks();
        }

        /// <summary>
        /// Creates requestet files and sets them to Options
        /// </summary>
        private void CreateFiles()
        {
            Directory.CreateDirectory(Options.DestinationPath);
            if (!string.IsNullOrWhiteSpace(Options.TemporaryPath))
                Directory.CreateDirectory(Options.TemporaryPath);
            else
                Options.TemporaryPath = Options.DestinationPath;
        }

        /// <summary>
        /// Instanziates the Lazy <see cref="ContentLength"/>.
        /// Called only one time
        /// </summary>
        /// <returns>A nullable Long that wants to contain the length of the content</returns>
        private long? GetContentLength()
        {
            long? length = null;
            if (ChunkInfo?.ContentLength != null)
                return ChunkInfo.ContentLength;
            try
            {
                using HttpResponseMessage res = HttpClient.Send(GetPresetRequestMessage(new(HttpMethod.Head, Url)), Token);
                if (!res.IsSuccessStatusCode)
                    return length;
                length = res.Content.Headers.ContentLength;
                length = SetRange(length);
                if (IsChunked)
                    ChunkInfo!.ContentLength = length;
            }
            catch (Exception) { }
            return length == 0 ? null : length;
        }

        /// <summary>
        /// Initializes the chunks if this is a chunked <see cref="LoadRequest"/>
        /// </summary>
        private void InitializeChunks()
        {
            if (Options.Chunks < 2)
                return;

            ChunkInfo = new()
            {
                Progress = Options.Progress,
                Chunks = new Chunk[Options.Chunks],
                Requests = new LoadRequest[Options.Chunks],
                RequestCompleated = Options.RequestCompleated
            };

            Options.RequestCompleated = null;
            Options.Progress = null;
            SetChunk();
            for (byte i = 1; i < Options.Chunks; i++)
                _ = new LoadRequest(ChunkInfo, this, i);
        }

        /// <summary>
        /// Sets the <see cref="Chunk"/> for this <see cref="LoadRequest"/>
        /// </summary>
        private void SetChunk()
        {
            TempExt = $"_{_chunkIndex}.chunk";
            ChunkInfo!.Requests[_chunkIndex] = this;
            ChunkInfo.Chunks[_chunkIndex] = new();

            if (ChunkInfo.Progress != null)
                ChunkInfo.Chunks[_chunkIndex].Progress = new Progress<float>(value => ChunkedRequestReport(value));
        }

        /// <summary>
        /// handles the Report of a chunked <see cref="LoadRequest"/>
        /// </summary>
        /// <param name="value"></param>
        private void ChunkedRequestReport(float value)
        {
            ChunkInfo!.Chunks[_chunkIndex].Percentage = value;
            if (ChunkInfo.ProgressCounter % Options.Chunks == 0)
            {
                double average = 0;
                for (int i = 0; i < ChunkInfo.Chunks.Length; i++)
                    average += ChunkInfo.Chunks[i].Percentage;

                ChunkInfo.Progress?.Report((float)average);
                ChunkInfo.ProgressCounter = 0;
            }
            else
                ChunkInfo.ProgressCounter++;
        }

        /// <summary>
        /// Constructor for chunked requests
        /// </summary>
        /// <param name="chunkInfo"><see cref="ChunkInfo{TNotify}"/> object that holds information</param>
        /// <param name="startRequest">The <see cref="LoadRequest"/> thta was the starting point</param>
        /// <param name="index">Chunk index of this <see cref="LoadRequest"/></param>
        private LoadRequest(ChunkInfo<string> chunkInfo, LoadRequest startRequest, byte index) : base(startRequest.Url, null)
        {
            ArgumentNullException.ThrowIfNull(chunkInfo);
            ChunkInfo = chunkInfo;
            _chunkIndex = index;
            Options = startRequest.Options;

            _contentLength = new(GetContentLength);

            SetChunk();
            if (Options.AutoStart)
                base.Start();
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
        private void LoadBytesWritten()
        {
            if (Options.Mode != LoadMode.Append || BytesWritten != 0 || !Options.FileName.Contains('.'))
                return;

            if (Options.ExcludedExtensions.Any(x => Options.FileName.EndsWith(x)))
                throw new InvalidOperationException($"FileName ends with invalid extension");

            SetDestinationPaths(Options.FileName);

            if (File.Exists(TmpDestination))
                BytesWritten = new FileInfo(TmpDestination).Length;
            else if (File.Exists(Destination))
            {
                if (IsChunked)
                    ChunkInfo!.BytesWritten = new FileInfo(Destination).Length;
                else
                {
                    BytesWritten = new FileInfo(Destination).Length;
                    IOManager.Move(Destination, TmpDestination);
                }
            }
        }

        private void SetDestinationPaths(string fileName)
        {
            TmpDestination = Path.Combine(Options.TemporaryPath, fileName + TempExt);
            Destination = Path.Combine(Options.DestinationPath, fileName);
        }

        /// <summary>
        /// Handels the Ddwnload of this <see cref="RequestObject"/>.
        /// </summary>
        /// <returns>A RequestReturn object</returns>
        protected override async Task<RequestReturn> RunRequestAsync()
        {
            RequestReturn result = new();

            try
            {
                result = await Load();
                result.FailedReturn?.Dispose();
                if (State == RequestState.Running)
                    if (result.Successful)
                        Options.Progress?.Report(1);
            }
            catch (Exception) { }
            finally
            {
                _timeoutCTS?.Dispose();
            }
            return result;
        }

        /// <summary>
        /// Handels the download process
        /// </summary>
        /// <returns>A RequestReturn object that indcates success</returns>
        private async Task<RequestReturn> Load()
        {
            await GetRequestIsFinished();
            if (_success.HasValue)
                return new(_success.Value, Destination, null);

            Stopwatch stopwatch = new();
            stopwatch.Start();

            HttpResponseMessage res = await SendHttpMenssage();

            if (_success.HasValue || State != RequestState.Running)
                return new(_success ?? false, Destination, res);

            res = await CheckRequestPossibilities(res);

            if (_success.HasValue || State != RequestState.Running)
                return new(_success ?? false, Destination, res);

            long startBytesWritten = BytesWritten;
            await WriterAsync(res);
            stopwatch.Stop();
            await CopyOrMerge();

            if (BytesWritten > 0)
                Options.Handler.AddSpeed((int)((BytesWritten - startBytesWritten) / stopwatch.Elapsed.TotalSeconds));

            return new(true, Destination, res);
        }

        private async Task<HttpResponseMessage> CheckRequestPossibilities(HttpResponseMessage res)
        {
            if (!res.IsSuccessStatusCode)
            {
                _success = false;
                return res;
            }

            bool isFileLoaded = BytesWritten != 0;
            bool isLengthLoaded = ContentLength != null;

            ProofContentLength(res.Content.Headers);
            SetFileInfo(res.Content.Headers);
            await GetRequestIsFinished();
            if (_success.HasValue || State != RequestState.Running)
                return res;

            //When ContentLegth Head Reaquest was forbidden: Reload the file and download only the range
            res = await ProofReload(res, isFileLoaded, isLengthLoaded);

            if (_success.HasValue || State != RequestState.Running)
                return res;
            ProofParttialContent(res.StatusCode);

            if (!File.Exists(Destination) && _chunkIndex == 0)
                IOManager.Create(Destination);

            return res;
        }

        private async Task<HttpResponseMessage> ProofReload(HttpResponseMessage res, bool isFileLoaded, bool isLengthLoaded)
        {
            if ((isFileLoaded || BytesWritten <= MIN_RELOAD) && (isLengthLoaded || !IsChunked && Range.Length == null))
                return res;

            if (!isLengthLoaded)
                _ = SetRange(res.Content.Headers.ContentLength);
            HttpResponseMessage newRes = await SendHttpMenssage();
            if (newRes.IsSuccessStatusCode)
            {
                res.Dispose();
                res = newRes;
                ProofContentLength(res.Content.Headers);
            }
            return res;
        }

        private void ProofParttialContent(System.Net.HttpStatusCode statusCode)
        {
            if (statusCode == System.Net.HttpStatusCode.PartialContent)
                return;
            if (IsChunked)
            {
                CancelChunkInfo();
                if (IsChunked)
                    Cancel();
            }

            IOManager.Create(TmpDestination);
            BytesWritten = 0;

        }

        /// <summary>
        /// Checks if the file can be moved or the chunked parts of the file can be merged.
        /// </summary>
        /// <returns>A awaitable Task</returns>
        private async Task CopyOrMerge()
        {
            //When the request is still running then move or merge
            if (State == RequestState.Running)
                if (!IsChunked)
                    IOManager.Move(TmpDestination, Destination);
                else
                {
                    ChunkInfo!.Chunks[_chunkIndex].IsFinished = true;
                    if (Array.TrueForAll(ChunkInfo.Chunks, chunk => chunk.IsFinished))
                    {
                        while (ChunkInfo.IsCopying)
                            await Task.Delay(500);
                        await IOManager.MergeChunks(ChunkInfo);
                        ChunkInfo.Progress?.Report(1f);
                    }
                    else if (Options.MergeWhileProgress)
                        await IOManager.MergeChunks(ChunkInfo);
                }
        }

        /// <summary>
        /// Writes the response to a file.
        /// </summary>
        /// <param name="res">Response of <see cref="HttpClient"/></param>
        /// <returns>A awaitable Task</returns>
        private async Task WriterAsync(HttpResponseMessage res)
        {
            using Stream? responseStream = await res.Content.ReadAsStreamAsync(Token);
            using FileStream? fs = new(Path.Combine(Options.TemporaryPath, Options.FileName + TempExt), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            IProgress<float>? progress = ContentLength != null ? (IsChunked ? ChunkInfo!.Chunks[_chunkIndex].Progress : Options.Progress) : null;
            while (State == RequestState.Running)
            {
                byte[]? buffer = new byte[1024];
                int bytesRead = await responseStream.ReadAsync(buffer, Token).ConfigureAwait(false);

                if (bytesRead == 0) break;
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead), Token);
                BytesWritten += bytesRead;
                progress?.Report((float)BytesWritten / (ContentLength!.Value + 10));
            }
            await fs.FlushAsync();
        }

        /// <summary>
        /// Cancel chunks if the <see cref="LoadRequest"/> can not be partial.
        /// </summary>
        private void CancelChunkInfo()
        {
            ArgumentNullException.ThrowIfNull(ChunkInfo);
            if (ChunkInfo.IsCopying)
                return;

            ChunkInfo.IsCopying = true;
            for (int i = 0; i < ChunkInfo.Requests.Length; i++)
            {
                LoadRequest req = ChunkInfo.Requests[i];
                if (File.Exists(req.TmpDestination))
                    File.Delete(req.TmpDestination);
                if (i == 0)
                {
                    req.Options.Progress = ChunkInfo.Progress;
                    req._contentLength = new(ChunkInfo.ContentLength);
                    req.Options.RequestCompleated = ChunkInfo.RequestCompleated;
                    req.ChunkInfo = null;
                    req.TempExt = ".part";
                    req.BytesWritten = 0;
                    if (File.Exists(req.TmpDestination))
                        File.Delete(req.TmpDestination);
                }
                else if (i != _chunkIndex)
                    req.Cancel();
            }
        }

        /// <summary>
        /// Creates a file out of the Information that were given
        /// </summary>
        /// <param name="headers">Response from the HttpClient</param>
        private void SetFileInfo(HttpContentHeaders headers)
        {
            string fileName = IOManager.GetFileName(headers, Options.FileName, _uri);

            SetDestinationPaths(fileName);

            switch (Options.Mode)
            {
                case LoadMode.Overwrite:
                    if (BytesWritten > 0)
                        break;
                    IOManager.Create(TmpDestination);
                    if (_chunkIndex == 0)
                        IOManager.Create(Destination);
                    break;
                case LoadMode.Create:
                    if ((!File.Exists(Destination) && !File.Exists(TmpDestination)) || BytesWritten > 0)
                        break;
                    int index = fileName.LastIndexOf('.');
                    string name = fileName;

                    for (int i = 1; (File.Exists(Destination) && _chunkIndex == 0) || File.Exists(TmpDestination); i++)
                    {
                        name = index != -1 ? fileName.Insert(index, $"({i})") : fileName + $"({i})";
                        SetDestinationPaths(name);
                    }
                    fileName = name;
                    if (_chunkIndex == 0)
                        IOManager.Create(Destination);
                    IOManager.Create(TmpDestination);
                    break;
                case LoadMode.Append:
                    Options.FileName = fileName;
                    LoadBytesWritten();
                    if (BytesWritten > ContentLength)
                    {
                        if (!IsChunked)
                        {
                            IOManager.Create(Destination);
                            IOManager.Create(TmpDestination);
                            BytesWritten = 0;
                        }
                        else throw new NotImplementedException();
                    }
                    break;
            }
            Options.FileName = fileName;
        }

        /// <summary>
        /// Looks if the pre set legth of the download file was correct
        /// </summary>
        /// <param name="headers">Header to get the information</param>
        private void ProofContentLength(HttpContentHeaders headers)
        {
            if (!headers.ContentLength.HasValue)
                return;

            long length = headers.ContentLength.Value;
            if (IsChunked)
            {
                long oldLength = ChunkInfo!.Chunks[_chunkIndex].ChunkLength;
                if (oldLength != length && oldLength != length + BytesWritten)
                    ChunkInfo.Chunks[_chunkIndex].ChunkLength = length;
            }
            else if ((ContentLength ?? 0) != length &&
           (ContentLength ?? 0) != length + BytesWritten)
                _contentLength = new(length);
        }

        /// <summary>
        /// Creates a HttpRequestMessage and send it.
        /// </summary>
        /// <returns>A response as <see cref="HttpResponseMessage"/></returns>
        private async Task<HttpResponseMessage> SendHttpMenssage()
        {
            HttpRequestMessage msg = GetPresetRequestMessage(new(HttpMethod.Get, Url));

            if (ContentLength != null)
            {
                Range loadRange;
                if (IsChunked)
                    loadRange = GetChunkedRange();
                else loadRange = Range;

                loadRange = new((loadRange.Start ?? 0) + BytesWritten, loadRange.End);
                if (loadRange.Start >= loadRange.End && loadRange.Length.HasValue)
                {
                    if (IsChunked)
                    {
                        _success = true;

                        ChunkInfo!.Chunks[_chunkIndex].Progress?.Report((float)ContentLength / ChunkInfo.Chunks[_chunkIndex].ChunkLength);
                        ChunkInfo!.Chunks[_chunkIndex].IsFinished = true;
                    }
                    else
                        _success = false;
                    return new();
                }
                if (loadRange.Length != null || loadRange.Start != 0)
                    msg.Headers.Range = new RangeHeaderValue(loadRange.Start, loadRange.End);
            }

            if (Options.Timeout.HasValue)
            {
                _timeoutCTS = CancellationTokenSource.CreateLinkedTokenSource(Token);
                _timeoutCTS.CancelAfter(Options.Timeout.Value);
            }

            return await HttpClient.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, _timeoutCTS?.Token ?? Token);
        }

        /// <summary>
        /// Get Range for chunked <see cref="LoadRequest">LoadRequests</see>
        /// </summary>
        /// <returns>A range thatm should be downloaded</returns>
        private Range GetChunkedRange()
        {
            if (ChunkInfo!.Chunks[_chunkIndex].Range.Length.HasValue)
                return ChunkInfo.Chunks[_chunkIndex].Range;

            long? contentLength = Range.Length == null ? ContentLength - (Range.Start ?? 0) : Range.Length;
            contentLength = contentLength > ContentLength ? ContentLength - (Range.Start ?? 0) : contentLength;

            long? chunkStart = (Range.Start ?? 0) + contentLength / Options.Chunks * _chunkIndex;
            long? chunkEnd = _chunkIndex + 1 == Options.Chunks ? Range.End :
                (Range.Start ?? 0) + ((_chunkIndex + 1) * (contentLength / Options.Chunks)) - 1;

            if (chunkEnd < BytesWritten + ChunkInfo.BytesWritten)
                return new(0, 0);
            chunkStart = chunkStart < BytesWritten + ChunkInfo.BytesWritten ? ChunkInfo.BytesWritten : chunkStart;

            Range range = new(chunkStart, chunkEnd);
            ChunkInfo.Chunks[_chunkIndex].Range = range;
            return range;
        }

        /// <summary>
        /// Indicates if the file was downloaded before.
        /// </summary>
        /// <returns>A bool to indicate if the file is same as in this <see cref="LoadRequest"/>.</returns>
        private async Task GetRequestIsFinished()
        {
            if (ContentLength.HasValue)
                if (IsChunked)
                {
                    if ((ChunkInfo!.BytesWritten >= ChunkInfo.Chunks[_chunkIndex].ChunkLength * (_chunkIndex + 1) && ChunkInfo.Chunks[_chunkIndex].ChunkLength != 0) ||
                        (ChunkInfo.Chunks[_chunkIndex].ChunkLength == BytesWritten && BytesWritten != 0))
                    {
                        if ((ChunkInfo!.BytesWritten >= ChunkInfo.Chunks[_chunkIndex].ChunkLength * (_chunkIndex + 1) && ChunkInfo.Chunks[_chunkIndex].ChunkLength != 0))
                            ChunkInfo.Chunks[_chunkIndex].IsCopied = true;
                        ChunkInfo!.Chunks[_chunkIndex].Progress?.Report((float)ContentLength / ChunkInfo.Chunks[_chunkIndex].ChunkLength);
                        await CopyOrMerge();
                        _success = true;
                    }
                }
                else if (BytesWritten == ContentLength)
                {
                    if (File.Exists(TmpDestination))
                        IOManager.Move(TmpDestination, Destination);
                    _success = true;
                }
        }

        /// <inheritdoc />
        public override void Start()
        {
            base.Start();
            if (IsChunked && _chunkIndex == 0)
                Array.ForEach(ChunkInfo!.Requests, request => request.Start());
        }

        /// <inheritdoc />
        public override void Pause()
        {
            base.Pause();
            if (IsChunked && _chunkIndex == 0)
                Array.ForEach(ChunkInfo!.Requests, request => request.Pause());
        }

        /// <inheritdoc />
        public override void Cancel()
        {
            base.Cancel();
            if (IsChunked && _chunkIndex == 0)
                Array.ForEach(ChunkInfo!.Requests, request => request.Cancel());
        }
    }
}
