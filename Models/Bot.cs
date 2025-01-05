namespace Marauder.Mini.Models;

public class Bot
{
    private Game? _game;
    private Player? _player;

    public void SetGame(Game? game) => _game = game;
    public void SetPlayer(Player? player) => _player = player;
}