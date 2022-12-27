using DownloaderLibrary.Base;
using DownloaderLibrary.Web.Request;

namespace DownloaderLibrary.Web
{
    /// <summary>
    /// ChunkInfo is a class that holds all Chunks for one bound of Requests that download one file
    /// </summary>
    /// <typeparam name="TNotify">The Notify return object for RequestCompleated event in RequestOptions</typeparam>
    internal class ChunkInfo<TNotify>
    {
        private int _isCopying = 0;

        public bool IsCopying
        {
            get { return (Interlocked.CompareExchange(ref _isCopying, 1, 1) == 1); }
            set
            {
                if (value) Interlocked.CompareExchange(ref _isCopying, 1, 0);
                else Interlocked.CompareExchange(ref _isCopying, 0, 1);
            }
        }

        internal Chunk[] Chunks { get; init; } = Array.Empty<Chunk>();
        internal LoadRequest[] Requests { get; init; } = Array.Empty<LoadRequest>();
        internal Notify<TNotify>? RequestCompleated { get; init; }
        internal IProgress<float>? Progress { get; init; }
        internal int ProgressCounter { get; set; }
        internal long BytesWritten { get; set; }
        internal long? ContentLength { get; set; } = null;
    }
}
