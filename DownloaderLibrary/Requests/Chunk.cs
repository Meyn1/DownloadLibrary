namespace DownloaderLibrary.Requests
{
    internal class Chunk
    {
        internal List<LoadRequest> Requests { get; init; } = new();
        internal byte Index { get; init; }
        internal PriorityQueue<string, byte> Destinations { get; init; } = new();
        internal List<float> ProgressList { get; init; } = new();
    }
}
