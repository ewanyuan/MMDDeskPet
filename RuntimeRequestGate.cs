using System.Text.Json;

namespace MMDDeskPet;

internal sealed class RuntimeRequestGate
{
    private readonly object _lock = new();
    private readonly Dictionary<string, Request> _pending = new(StringComparer.Ordinal);
    private Request? _activeLoad;

    internal sealed class Request
    {
        public required string RequestId { get; init; }

        public required string Operation { get; init; }

        public required string ResponseType { get; init; }

        public bool IsLoad { get; init; }

        public TaskCompletionSource<JsonElement> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Cancel { get; } = new();

        public bool Superseded { get; set; }

        public string? CancelReason { get; set; }
    }

    public Request Begin(string operation, string responseType, bool supersedePreviousLoad)
    {
        Request? previous = null;
        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Operation = operation,
            ResponseType = responseType,
            IsLoad = supersedePreviousLoad,
        };

        lock (_lock)
        {
            if (supersedePreviousLoad && _activeLoad is { } active)
            {
                active.Superseded = true;
                active.CancelReason = "superseded-by-" + operation;
                _pending.Remove(active.RequestId);
                _activeLoad = null;
                previous = active;
            }

            _pending[request.RequestId] = request;
            if (supersedePreviousLoad)
            {
                _activeLoad = request;
            }
        }

        if (previous is not null)
        {
            SignalCancelled(previous);
        }

        return request;
    }

    public bool TryComplete(string type, string? requestId, JsonElement payload)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return false;
        }

        Request? request;
        lock (_lock)
        {
            if (!_pending.TryGetValue(requestId, out request)
                || !string.Equals(request.ResponseType, type, StringComparison.Ordinal))
            {
                return false;
            }

            _pending.Remove(requestId);
        }

        return request.Completion.TrySetResult(payload.Clone());
    }

    public IReadOnlyList<Request> FailByRuntimeError(string? requestId, Exception error)
    {
        List<Request> failed = [];
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(requestId)
                && _pending.TryGetValue(requestId, out var match))
            {
                _pending.Remove(requestId);
                if (ReferenceEquals(_activeLoad, match))
                {
                    _activeLoad = null;
                }

                failed.Add(match);
            }
            else if (string.IsNullOrWhiteSpace(requestId) && _pending.Count > 0)
            {
                failed.AddRange(_pending.Values);
                _pending.Clear();
                _activeLoad = null;
            }
        }

        foreach (var request in failed)
        {
            request.Completion.TrySetException(error);
        }

        return failed;
    }

    public IReadOnlyList<Request> CancelAll(string reason)
    {
        List<Request> pending;
        lock (_lock)
        {
            pending = _pending.Values.ToList();
            _pending.Clear();
            _activeLoad = null;
        }

        foreach (var request in pending)
        {
            request.Superseded = true;
            request.CancelReason = reason;
            SignalCancelled(request);
        }

        return pending;
    }

    public void Release(Request request)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(request.RequestId, out var current)
                && ReferenceEquals(current, request))
            {
                _pending.Remove(request.RequestId);
            }

            if (ReferenceEquals(_activeLoad, request))
            {
                _activeLoad = null;
            }
        }

        request.Cancel.Dispose();
    }

    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    private static void SignalCancelled(Request request)
    {
        request.Completion.TrySetException(new OperationCanceledException(request.CancelReason ?? "cancelled"));
        try
        {
            request.Cancel.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The waiter already left and disposed its cancellation source.
        }
    }
}

internal static class StartupModelPolicy
{
    public static bool ShouldAutoLoadBundledModel => false;

    public static bool ShouldRestoreLastModel(string? path, Func<string, bool> fileExists)
    {
        return !string.IsNullOrWhiteSpace(path) && fileExists(path);
    }

    public static bool ShouldLoadFirstLibraryModel(
        string? lastModelPath,
        Func<string, bool> fileExists,
        int libraryModelCount)
    {
        return libraryModelCount >= 1 && !ShouldRestoreLastModel(lastModelPath, fileExists);
    }
}
