namespace DownloaderLibrary.Base
{
    /// <summary>
    /// Stores options that configure the degree of max parallism in for the channel
    /// </summary>
    internal class ParallelChannelOptions : ParallelOptions
    {
        private int _maxDegreeOfParallelism = Environment.ProcessorCount;

        public event EventHandler<int>? DegreeOfParallelismChangedDelta;

        public PauseToken EasyEndToken { get; set; }

        public ParallelChannelOptions() => base.MaxDegreeOfParallelism = int.MaxValue;

        public new int MaxDegreeOfParallelism
        {
            get => _maxDegreeOfParallelism;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism));
                if (value == _maxDegreeOfParallelism) return;
                int delta = value - _maxDegreeOfParallelism;
                DegreeOfParallelismChangedDelta?.Invoke(this, delta);
                _maxDegreeOfParallelism = value;
            }
        }
    }
}
