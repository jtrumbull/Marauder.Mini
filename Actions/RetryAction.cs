namespace Marauder.Mini.Actions;

/// <summary>
/// Wraps an action with retry logic to handle transient failures.
/// </summary>
public class RetryAction : ActionBase
{
    private readonly IAction _innerAction;
    private readonly int _maxRetries;
    private readonly TimeSpan _retryDelay;
    private int _currentAttempt;

    /// <summary>
    /// Gets the number of retry attempts made so far.
    /// </summary>
    public int CurrentAttempt => _currentAttempt;

    /// <summary>
    /// Gets the remaining number of retry attempts.
    /// </summary>
    public int RemainingAttempts => Math.Max(0, _maxRetries - _currentAttempt);

    public RetryAction(
        IAction innerAction,
        int maxRetries,
        TimeSpan? retryDelay = null,
        ActionOptions? options = null)
        : base($"Retry({innerAction.Name})", options)
    {
        _innerAction = innerAction ?? throw new ArgumentNullException(nameof(innerAction));
        _maxRetries = maxRetries > 0 ? maxRetries : throw new ArgumentOutOfRangeException(nameof(maxRetries));
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(500);
    }

    public override ValueTask<bool> CanExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        return _innerAction.CanExecuteAsync(context, cancellationToken);
    }

    protected override async Task<ActionResult> ExecuteCoreAsync(
        IReadOnlyDictionary<string, object>? context,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        _currentAttempt = 0;
        ActionResult? lastResult = null;
        var exceptions = new List<Exception>();

        do
        {
            _currentAttempt++;
            
            try
            {
                // Scale progress to account for potential retries
                var retryProgress = new Progress<double>(p =>
                {
                    var scaledProgress = (_currentAttempt - 1 + p) / Math.Min(_currentAttempt + RemainingAttempts, _maxRetries);
                    progress?.Report(scaledProgress);
                });

                // Attempt the action
                lastResult = await _innerAction.ExecuteAsync(context, retryProgress, cancellationToken);
                
                if (lastResult.Value.Success)
                    return lastResult.Value;

                // If we still have retries, log the failure and continue
                if (RemainingAttempts > 0)
                {
                    LogRetryAttempt(lastResult.Value.Message);
                    
                    // Wait before retrying, unless we're cancelled
                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exceptions.Add(ex);
                
                if (RemainingAttempts > 0)
                {
                    LogRetryAttempt(ex.Message);
                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }

        } while (RemainingAttempts > 0);

        // If we're here, we've exhausted all retries
        var failureContext = new Dictionary<string, object>
        {
            ["attempts"] = _currentAttempt,
            ["exceptions"] = exceptions
        };

        if (lastResult?.Context != null)
        {
            foreach (var kvp in lastResult.Value.Context)
            {
                failureContext[kvp.Key] = kvp.Value;
            }
        }

        return ActionResult.Failed(
            $"Action failed after {_currentAttempt} attempts. Last error: {lastResult?.Message ?? exceptions.LastOrDefault()?.Message}",
            failureContext);
    }

    private void LogRetryAttempt(string? errorMessage)
    {
        var message = $"Attempt {_currentAttempt} failed{(errorMessage != null ? $": {errorMessage}" : "")}.";
        if (RemainingAttempts > 0)
            message += $" Retrying in {_retryDelay.TotalMilliseconds}ms ({RemainingAttempts} attempts remaining)";
    }

    public override async ValueTask<bool> CancelAsync()
    {
        var cancelled = await base.CancelAsync();
        if (!cancelled) return false;

        // Cancel the inner action if it's running
        if (_innerAction.Status == ActionStatus.Running)
            await _innerAction.CancelAsync();

        return true;
    }

    public override async ValueTask DisposeAsync()
    {
        await _innerAction.DisposeAsync();
        await base.DisposeAsync();
    }
}

/// <summary>
/// Extension methods for retry behavior configuration.
/// </summary>
public static class RetryActionExtensions
{
    /// <summary>
    /// Configures exponential backoff for retry delays.
    /// </summary>
    public static RetryAction WithExponentialBackoff(
        this RetryAction action,
        TimeSpan initialDelay,
        double backoffFactor = 2.0)
    {
        // Here we could implement exponential backoff by creating a new RetryAction
        // with custom delay calculation. This is a placeholder for the concept.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Configures retry only for specific exception types.
    /// </summary>
    public static RetryAction RetryOn<TException>(this RetryAction action)
        where TException : Exception
    {
        // Here we could implement selective retry based on exception type.
        // This is a placeholder for the concept.
        throw new NotImplementedException();
    }
}