namespace Marauder.Mini.Actions;

public interface IAction
{
    void Run();
    Task RunAsync(CancellationToken token);
}

public class BaseAction : IAction
{
    public virtual void Run() => RunAsync(CancellationToken.None).Wait();

    public virtual async Task RunAsync(CancellationToken token)
    {
        await Task.Delay(0);
        throw new NotImplementedException();
    }
}

public class ActionSequence : Queue<IAction>
{
    public async Task RunAsync(CancellationToken token)
    {
        while(TryDequeue(out IAction? action))
        {
            token.ThrowIfCancellationRequested();
            await action.RunAsync(token);
        }
    }
}