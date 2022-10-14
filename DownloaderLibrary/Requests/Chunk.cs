namespace DownloaderLibrary.Requests
{
    internal class Chunk
    {
        internal List<LoadRequest> Requests { get; init; } = new();
        internal byte Index { get; init; }
        internal PriorityQueue<string, byte> Destinations { get; init; } = new();
        internal float[]? Progress { get; init; }
        internal bool IsRangeSet { get; set; }
        internal Action<object>? OnCompleated { get; init; }
    }
}
