using DownloaderLibrary.Requests;

namespace DownloaderLibrary.Utilities
{
    internal class Chunk
    {
        internal List<LoadRequest> Requests { get; init; } = new();
        internal byte Index { get; init; }
        internal string[] Destinations { get; init; } = Array.Empty<string>();
        internal float[]? Progress { get; init; }
        internal bool IsRangeSet { get; set; }
        internal NotifyObject? OnCompleated { get; init; }
        internal IProgress<float>? MainProgress { get; init; }
    }
}
