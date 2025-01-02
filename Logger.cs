namespace Marauder.Mini;

public static class Logger
{
    public static void Stat(string name, object? value)
    {
        Console.Write($"{name}: ");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine(value);
        Console.ResetColor();
    }
}