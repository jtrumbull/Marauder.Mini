using Marauder.Mini.Enums;
using Marauder.Mini.Models;

namespace Marauder.Mini.Actions;

public static class BotActions
{
    public static MoveAction Move()
    {
        var move = new MoveAction();
        return move;
    }

    public static IAction MoveTo(Area area)
    {
        var sequence = new CompositeAction(new[]
        {
            new MoveAction(),
            new MoveAction(),
            new MoveAction()
        });
        return sequence;
    }

    public static MoveAction MoveTo(Position pos)
    {
        var move = new MoveAction();
        return move;
    }
    public static void Attack(Monster monster) => Task.Delay(0);

    public static void Kill(Monster monster) => Task.Delay(0);
}