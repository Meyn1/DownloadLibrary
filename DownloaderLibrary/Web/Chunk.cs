namespace DownloaderLibrary.Web
{
    internal record Chunk
    {
        internal bool IsFinished { get; set; }
        internal bool IsCopied { get; set; }
        internal float Percentage { get; set; }
        internal Range Range { get; set; }
        internal IProgress<float>? Progress { get; set; }
        internal long ChunkLength { get; set; }
    }
}
