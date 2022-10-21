namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Stores options that configure the DynamicParallelForEachAsync method.
    /// </summary>
    internal class DynamicParallelOptions : ParallelOptions
    {
        private int _maxDegreeOfParallelism = Environment.ProcessorCount;


        public event EventHandler<int>? DegreeOfParallelismChangedDelta;

        public CancellationToken EasyEndToken { get; set; }

        public DynamicParallelOptions() => base.MaxDegreeOfParallelism = int.MaxValue;

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
