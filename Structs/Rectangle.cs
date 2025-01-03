namespace Marauder.Mini.Structs;

public struct Rectangle
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}