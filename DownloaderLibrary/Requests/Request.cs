﻿namespace DownloaderLibrary.Requests
{
    /// <summary>
    /// Indicates the state of a <see cref="Request"/>.
    /// </summary>
    public enum RequestState
    {
        /// <summary>
        /// <see cref="Request"/> can be started.
        /// </summary>
        Available,
        /// <summary>
        /// <see cref="Request"/> is running.
        /// </summary>
        Running,
        /// <summary>
        /// <see cref="Request"/> is sucessfuly compleated.
        /// </summary>
        Compleated,
        /// <summary>
        /// <see cref="Request"/> is paused.
        /// </summary>
        Onhold,
        /// <summary>
        /// <see cref="Request"/> is cancelled.
        /// </summary>
        Cancelled,
        /// <summary>
        /// <see cref="Request"/> failed.
        /// </summary>
        Failed
    }
    /// <summary>
    /// A <see cref="Request"/> object that can be managed by the <see cref="RequestHandler"/>.
    /// </summary>
    public abstract class Request : IDisposable
    {
        /// <summary>
        /// If this object is disposed of.
        /// </summary>
        private bool _disposed;
        /// <summary>
        /// How often this <see cref="Request"/> failded.
        /// </summary>
        private byte _tryCounter = 0;
        /// <summary>
        /// The <see cref="CancellationTokenSource"/> for this object.
        /// </summary>
        private CancellationTokenSource _cts;
        /// <summary>
        /// The <see cref="RequestState"/> of this <see cref="Request"/>.
        /// </summary>
        private RequestState _state = RequestState.Onhold;
        /// <summary>
        /// <see cref="System.Threading.Tasks.Task"/> that indicates of this <see cref="Request"/> finished.
        /// </summary>
        private readonly TaskCompletionSource _isFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);


        /// <summary>
        /// The <see cref="RequestOptions"/> of this object.
        /// </summary>
        internal RequestOptions Options { get; init; }

        /// <summary>
        /// <see cref="CancellationToken"/> that indicates if this <see cref="Request"/> was cancelled.
        /// </summary>
        protected CancellationToken Token => _cts.Token;
        /// <summary>
        /// <see cref="String"/> that holds the URL of the <see cref="Request"/>.
        /// </summary>
        protected readonly string _url;
        /// <summary>
        /// <see cref="System.Threading.Tasks.Task"/> that indicates of this <see cref="Request"/> finished.
        /// </summary>
        public Task Task => _isFinished.Task;
        /// <summary>
        /// Delays the start of the <see cref="Request"/> on every Start call for the specified number of milliseconds.
        /// </summary>
        public int DeployDelay { get => Options.DeployDelay; set => Options.DeployDelay = value; }

        /// <summary>
        /// The <see cref="RequestState"/> of this <see cref="Request"/>.
        /// </summary>
        public RequestState State { get => _state; protected set => _state = _state == RequestState.Compleated || _state == RequestState.Failed ? _state : value; }

        /// <summary>
        /// Consructor of the <see cref="Request"/> class 
        /// </summary>
        /// <param name="url">URL that the <see cref="Request"/> calls</param>
        /// <param name="options">Options to modify the <see cref="Request"/></param>
        public Request(string url = "", RequestOptions? options = null)
        {
            _url = url;
            Options = options != null ? new(options) : new();
            if (Options.CancellationToken.HasValue)
                _cts = CancellationTokenSource.CreateLinkedTokenSource(Options.RequestHandler.CT, Options.CancellationToken.Value);
            else
                _cts = CancellationTokenSource.CreateLinkedTokenSource(Options.RequestHandler.CT);
        }

        /// <summary>
        /// Cancel the <see cref="Request"/>
        /// </summary>
        /// /// <exception cref="AggregateException"></exception>
        /// /// <exception cref="ObjectDisposedException"></exception>
        /// /// <exception cref="InvalidOperationException"></exception>
        public void Cancel()
        {
            State = RequestState.Cancelled;
            _cts.Cancel();
            _isFinished.SetResult();
        }
        /// <summary>
        /// Dispose the <see cref="Request"/>. 
        /// Will be called automaticly ba the <see cref="RequestHandler"/>.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void Dispose()
        {
            if (State == RequestState.Running)
                Cancel();
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Wait to finish this <see cref="Request"/>.
        /// </summary>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Wait() => Task.Wait();

        /// <summary>
        /// Dispose this object if dispose is true.
        /// </summary>
        /// <param name="dispose">Indicates if this object sould be disposed</param>
        protected virtual void Dispose(bool dispose)
        {
            if (_disposed)
                return;
            if (dispose)
                _cts.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Runs the <see cref="Request"/> that was created out this object
        /// </summary>
        internal async Task StartRequestAsync()
        {
            lock (this)
            {
                if (State != RequestState.Available || Options.CancellationToken.HasValue && Options.CancellationToken.Value.IsCancellationRequested)
                    return;

                if (_cts.IsCancellationRequested)
                {
                    if (Options.RequestHandler.CT.IsCancellationRequested)
                        return;
                    _cts.Dispose();
                    if (Options.CancellationToken.HasValue)
                        _cts = CancellationTokenSource.CreateLinkedTokenSource(Options.RequestHandler.CT, Options.CancellationToken.Value);
                    else
                        _cts = CancellationTokenSource.CreateLinkedTokenSource(Options.RequestHandler.CT);
                }
                State = RequestState.Running;
            }

            bool compleated = false;
            try
            {
                compleated = await RunRequestAsync();
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception) { }

            if (State == RequestState.Running)
                if (compleated)
                    State = RequestState.Compleated;
                else if (Token.IsCancellationRequested)
                    if (Options.CancellationToken.HasValue && Options.CancellationToken.Value.IsCancellationRequested)
                        State = RequestState.Cancelled;
                    else
                        State = RequestState.Available;
                else if (_tryCounter++ < Options.TryCounter)
                    State = RequestState.Available;
                else
                    State = RequestState.Failed;

            if (State == RequestState.Compleated || State == RequestState.Failed || State == RequestState.Cancelled)
                _isFinished.TrySetResult();
        }

        /// <summary>
        /// Handles the <see cref="Request"/> that the <see cref="HttpClient"/> sould start.
        /// </summary>
        /// <returns>A bool that indicates if the <see cref="Request"/> was succesful.</returns>
        protected abstract Task<bool> RunRequestAsync();

        /// <summary>
        /// Start the <see cref="Request"/> if it is not yet started or paused.
        /// </summary>
        public void Start()
        {
            if (State != RequestState.Onhold)
                return;
            State = RequestState.Available;
            if (DeployDelay > 0)
                Task.Run(() =>
                {
                    Task.Delay(DeployDelay);
                    if (State == RequestState.Available)
                        Options.RequestHandler.RunRequests(this);
                });
            else
                Options.RequestHandler.RunRequests(this);
        }
        /// <summary>
        /// Set the <see cref="Request"/> on hold.
        /// </summary>
        public void Pause() => State = RequestState.Onhold;
    }
}
