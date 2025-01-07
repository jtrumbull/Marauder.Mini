using System.Collections.Immutable;

namespace Marauder.Mini.Actions;

/// <summary>
/// Represents a sequence of actions that should be executed in order.
/// Implements the Composite pattern for IAction.
/// </summary>
public class CompositeAction : ActionBase
{
    private readonly ImmutableArray<IAction> _actions;
    
    public CompositeAction(IEnumerable<IAction> actions, ActionOptions? options = null) 
        : base("Composite", options)
    {
        _actions = actions.ToImmutableArray();
        
        if (_actions.Length == 0)
            throw new ArgumentException("At least one action is required", nameof(actions));
    }

    public override async ValueTask<bool> CanExecuteAsync(
        IReadOnlyDictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        // Check if all actions can execute
        foreach (var action in _actions)
        {
            if (!await action.CanExecuteAsync(context, cancellationToken))
                return false;
        }
        
        return true;
    }

    protected override async Task<ActionResult> ExecuteCoreAsync(
        IReadOnlyDictionary<string, object>? context,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var currentContext = context;
        var progressPerAction = 1.0 / _actions.Length;
        var currentProgress = 0.0;

        // Execute each action in sequence
        for (var i = 0; i < _actions.Length; i++)
        {
            var action = _actions[i];
            
            // Create a progress scope for this action
            var actionProgress = new Progress<double>(p =>
            {
                var actionContribution = p * progressPerAction;
                progress?.Report(currentProgress + actionContribution);
            });

            // Execute the action
            var result = await action.ExecuteAsync(
                currentContext,
                actionProgress,
                cancellationToken);

            // Update progress
            currentProgress += progressPerAction;
            progress?.Report(currentProgress);

            if (!result.Success && !Options.ContinueOnFailure)
            {
                return ActionResult.Failed(
                    $"Action {i + 1} ({action.Name}) failed: {result.Message}",
                    result.Context);
            }

            // Pass context to next action
            currentContext = result.Context;
        }

        return ActionResult.Succeeded(
            "All composite actions completed",
            currentContext);
    }

    public override async ValueTask<bool> CancelAsync()
    {
        var cancelled = await base.CancelAsync();
        if (!cancelled) return false;

        // Cancel any running actions
        foreach (var action in _actions)
        {
            if (action.Status == ActionStatus.Running)
                await action.CancelAsync();
        }

        return true;
    }

    public override async ValueTask DisposeAsync()
    {
        // Dispose all child actions
        foreach (var action in _actions)
        {
            await action.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}

/// <summary>
/// Factory methods for creating composite actions.
/// </summary>
public static class CompositeActionFactory
{
    /// <summary>
    /// Creates a composite action that executes all actions in sequence.
    /// </summary>
    public static IAction Sequence(params IAction[] actions) =>
        new CompositeAction(actions);

    /// <summary>
    /// Creates a composite action that executes all actions in sequence,
    /// continuing even if some actions fail.
    /// </summary>
    public static IAction SequenceWithContinuation(params IAction[] actions) =>
        new CompositeAction(actions, new ActionOptions { ContinueOnFailure = true });
}