namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Class to manage and merge more than one <see cref="Request"/>.
    /// </summary>
    public class RequestContainer
    {
        private readonly List<Request> _requests = new();
        private bool _isrunning = true;
        private bool _isCanceled = false;

        /// <summary>
        /// Constructor to merge <see cref="Request"/> together
        /// </summary>
        /// <param name="requests"><see cref="Request"/>s to merge</param>
        public RequestContainer(params Request[] requests)
        {
            foreach (Request? request in requests)
                Add(request);
        }

        /// <summary>
        /// Creates a new <see cref="RequestContainer"/> that megres  <see cref="RequestContainer"/> together.
        /// </summary>
        /// <param name="requestContainer">Fist <see cref="RequestContainer"/>to merge</param>
        /// <param name="requestContainers">Other <see cref="RequestContainer"/> to merge</param>
        /// <returns></returns>
        public static RequestContainer MergeContainers(RequestContainer requestContainer, params RequestContainer[] requestContainers)
        {
            List<Request> requests = new();
            foreach (RequestContainer requestC in requestContainers)
                foreach (Request request in requestC._requests)
                    requests.Add(request);
            foreach (Request request in requestContainer._requests)
                requests.Add(request);
            return new RequestContainer(requests.ToArray());
        }

        /// <summary>
        /// Main Contructor for <see cref="RequestContainer"/>.
        /// </summary>
        public RequestContainer()
        { }
        /// <summary>
        /// Adds a <see cref="Request"/> to the <see cref="RequestContainer"/>.
        /// </summary>
        /// <param name="request">The <see cref="Request"/> to add.</param>
        public void Add(Request request)
        {
            if (_isCanceled)
                request.Cancel();
            else if (!_isrunning)
                request.Pause();

            _requests.Add(request);
        }
        /// <summary>
        /// Cancel all <see cref="Request"/> in container
        /// </summary>
        public void Cancel()
        {
            _isCanceled = true;
            foreach (Request? request in _requests)
                request.Cancel();
        }
        /// <summary>
        /// Starts all <see cref="Request"/> if they are on hold
        /// </summary>
        public void Start()
        {
            _isrunning = true;
            foreach (Request? request in _requests)
                request.Start();
        }
        /// <summary>
        /// Put every <see cref="Request"/> in Container on hold
        /// </summary>
        public void Pause()
        {
            _isrunning = false;
            foreach (Request? request in _requests)
                request.Pause();
        }
        /// <summary>
        /// Waits to finish every <see cref="Request"/>
        /// </summary>
        public void WaitToFinishAll() => Task.WaitAll(_requests.Select(x => x.Task).ToArray());
    }
}

