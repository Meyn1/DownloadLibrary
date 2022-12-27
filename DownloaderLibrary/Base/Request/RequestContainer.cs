namespace DownloaderLibrary.Base.Request
{
    /// <summary>
    /// Class to manage and merge more than one <see cref="RequestObject"/>.
    /// </summary>
    public class RequestContainer
    {
        private readonly List<RequestObject> _requests = new();
        private bool _isrunning = true;
        private bool _isCanceled = false;

        /// <summary>
        /// Constructor to merge <see cref="RequestObject"/> together
        /// </summary>
        /// <param name="requests"><see cref="RequestObject"/>s to merge</param>
        public RequestContainer(params RequestObject[] requests) => AddRange(requests);

        /// <summary>
        /// Get all <see cref="RequestObject"/> in this Container
        /// </summary>
        /// <returns>returns a <see cref="RequestObject"/> array</returns>
        public RequestObject[] GetRequests() => _requests.ToArray();

        /// <summary>
        /// Creates a new <see cref="RequestContainer"/> that megres  <see cref="RequestContainer"/> together.
        /// </summary>
        /// <param name="requestContainers">Other <see cref="RequestContainer"/> to merge</param>
        /// <returns></returns>
        public static RequestContainer MergeContainers(params RequestContainer[] requestContainers)
        {
            List<RequestObject> requests = new();
            Array.ForEach(requestContainers, requestContainer => requests.AddRange(requestContainer._requests));
            return new RequestContainer(requests.ToArray());
        }

        /// <summary>
        /// Main Contructor for <see cref="RequestContainer"/>.
        /// </summary>
        public RequestContainer()
        { }

        /// <summary>
        /// Adds a <see cref="RequestObject"/> to the <see cref="RequestContainer"/>.
        /// </summary>
        /// <param name="request">The <see cref="RequestObject"/> to add.</param>
        public void Add(RequestObject request)
        {
            if (_isCanceled)
                request.Cancel();
            else if (!_isrunning)
                request.Pause();

            _requests.Add(request);
        }

        /// <summary>
        /// Adds a range <see cref="RequestObject"/> to the <see cref="RequestContainer"/>.
        /// </summary>
        /// <param name="requests">The <see cref="RequestObject"/> to add.</param>
        public void AddRange(params RequestObject[] requests)
        {
            if (_isCanceled)
                Array.ForEach(requests, request => request.Cancel());
            else if (!_isrunning)
                Array.ForEach(requests, request => request.Pause());

            _requests.AddRange(requests);
        }

        /// <summary>
        /// Removes a <see cref="RequestObject"/> from this container.
        /// </summary>
        /// <param name="requests">Request to remove</param>
        public void Remove(params RequestObject[] requests) => Array.ForEach(requests, request => _requests.Remove(request));

        /// <summary>
        /// Cancel all <see cref="Request"/> in container
        /// </summary>
        public void Cancel()
        {
            _isCanceled = true;
            _requests.ForEach(request => request.Cancel());
        }

        /// <summary>
        /// Starts all <see cref="RequestObject"/> if they are on hold
        /// </summary>
        public void Start()
        {
            _isrunning = true;
            foreach (RequestObject? request in _requests)
                request.Start();
        }
        /// <summary>
        /// Put every <see cref="RequestObject"/> in Container on hold
        /// </summary>
        public void Pause()
        {
            _isrunning = false;
            foreach (RequestObject? request in _requests)
                request.Pause();
        }
        /// <summary>
        /// Waits to finish every <see cref="RequestObject"/>
        /// </summary>
        public void WaitToFinishAll() => Task.WaitAll(_requests.Select(x => x.Task).ToArray());
    }
}
