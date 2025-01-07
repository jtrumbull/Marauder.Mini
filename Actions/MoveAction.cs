
using Marauder.Mini.Enums;

namespace Marauder.Mini.Actions;

public class MoveAction() : ActionBase("move")
{
    public override ValueTask<bool> CanExecuteAsync(IReadOnlyDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task RunAsync(Area area, CancellationToken token)
    {
        bool entranceIsNearby = true;
        int x = 0;
        int y = 0;
        if(entranceIsNearby)
            await RunAsync(new Position(x, y), token);
    }

    public async Task RunAsync(Position position, CancellationToken token)
    {
        await Task.Delay(0);
    }

    public async Task RunAsync(int x, int y, CancellationToken token)
        => await RunAsync(new Position(x, y), token);

    protected override Task<ActionResult> ExecuteCoreAsync(IReadOnlyDictionary<string, object>? context, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}