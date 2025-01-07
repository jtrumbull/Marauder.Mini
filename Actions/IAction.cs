namespace Marauder.Mini.Actions;

/// <summary>
/// Represents the result of executing a bot action.
/// </summary>
public readonly record struct ActionResult
{
    /// <summary>
    /// Whether the action was successful.
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// Optional message providing details about the action result.
    /// </summary>
    public string? Message { get; init; }
    
    /// <summary>
    /// Any state or data that should be passed to subsequent actions.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }

    public static ActionResult Succeeded(string? message = null, IReadOnlyDictionary<string, object>? context = null) =>
        new() { Success = true, Message = message, Context = context };

    public static ActionResult Failed(string? message = null, IReadOnlyDictionary<string, object>? context = null) =>
        new() { Success = false, Message = message, Context = context };
}

/// <summary>
/// Describes the current state of an action's execution.
/// </summary>
public enum ActionStatus
{
    /// <summary>
    /// Action has not started execution.
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// Action is currently executing.
    /// </summary>
    Running,
    
    /// <summary>
    /// Action completed successfully.
    /// </summary>
    Completed,
    
    /// <summary>
    /// Action failed to complete.
    /// </summary>
    Failed,
    
    /// <summary>
    /// Action was cancelled before completion.
    /// </summary>
    Cancelled
}

/// <summary>
/// Configuration options for action execution.
/// </summary>
public record ActionOptions
{
    /// <summary>
    /// Maximum time to wait for the action to complete.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// Whether to continue execution if the action fails.
    /// </summary>
    public bool ContinueOnFailure { get; init; }
}

/// <summary>
/// Interface for bot actions that can be sequenced into more complex behaviors.
/// </summary>
public interface IAction : IAsyncDisposable
{
    /// <summary>
    /// Gets the unique identifier for this action instance.
    /// </summary>
    Guid Id { get; }
    
    /// <summary>
    /// Gets the name of this action type.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the current status of the action.
    /// </summary>
    ActionStatus Status { get; }
    
    /// <summary>
    /// Gets the options configured for this action.
    /// </summary>
    ActionOptions Options { get; }

    /// <summary>
    /// Checks if the action can be executed in the current game state.
    /// </summary>
    /// <param name="context">Optional context from previous actions.</param>
    /// <param name="cancellationToken">Token to cancel the check.</param>
    /// <returns>True if the action can be executed, false otherwise.</returns>
    ValueTask<bool> CanExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the action.
    /// </summary>
    /// <param name="context">Optional context from previous actions.</param>
    /// <param name="progress">Optional progress reporting.</param>
    /// <param name="cancellationToken">Token to cancel execution.</param>
    /// <returns>The result of the action execution.</returns>
    Task<ActionResult> ExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to cancel the currently executing action.
    /// </summary>
    /// <returns>True if cancellation was initiated, false if the action cannot be cancelled.</returns>
    ValueTask<bool> CancelAsync();
}

/// <summary>
/// Extension methods for IAction implementations.
/// </summary>
public static class ActionExtensions
{
    /// <summary>
    /// Creates a new action that executes this action followed by another action.
    /// </summary>
    public static IAction Then(this IAction first, IAction second)
        => new CompositeAction(new[] { first, second });
    
    /// <summary>
    /// Creates a new action that executes this action followed by multiple other actions.
    /// </summary>
    public static IAction ThenMany(this IAction first, params IAction[] others)
        => new CompositeAction(new[] { first }.Concat(others));
    
    /// <summary>
    /// Creates a new action that retries this action until successful or max retries reached.
    /// </summary>
    public static IAction WithRetry(this IAction action, int maxRetries, TimeSpan? retryDelay = null)
        => new RetryAction(action, maxRetries, retryDelay);
}

/// <summary>
/// Base implementation of IAction providing common functionality.
/// </summary>
public abstract class ActionBase : IAction
{
    private readonly CancellationTokenSource _cts = new();
    private ActionStatus _status = ActionStatus.NotStarted;
    
    protected ActionBase(string name, ActionOptions? options = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Options = options ?? new ActionOptions();
    }

    public Guid Id { get; }
    public string Name { get; }
    public ActionStatus Status 
    {
        get => _status;
        protected set => _status = value;
    }
    public ActionOptions Options { get; }

    public abstract ValueTask<bool> CanExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);

    public async Task<ActionResult> ExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Status == ActionStatus.Running)
            throw new InvalidOperationException($"Action {Name} is already running");

        Status = ActionStatus.Running;
        
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cts.Token);

            var result = await ExecuteCoreAsync(context, progress, linkedCts.Token);
            Status = result.Success ? ActionStatus.Completed : ActionStatus.Failed;
            return result;
        }
        catch (OperationCanceledException)
        {
            Status = ActionStatus.Cancelled;
            return ActionResult.Failed($"Action {Name} was cancelled");
        }
        catch (Exception ex)
        {
            Status = ActionStatus.Failed;
            return ActionResult.Failed($"Action {Name} failed: {ex.Message}");
        }
    }

    protected abstract Task<ActionResult> ExecuteCoreAsync(
        IReadOnlyDictionary<string, object>? context,
        IProgress<double>? progress,
        CancellationToken cancellationToken);

    public virtual ValueTask<bool> CancelAsync()
    {
        if (Status != ActionStatus.Running)
            return new ValueTask<bool>(false);

        _cts.Cancel();
        return new ValueTask<bool>(true);
    }

    public virtual async ValueTask DisposeAsync()
    {
        await CancelAsync();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}