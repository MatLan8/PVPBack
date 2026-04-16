namespace PVPBack.Core.Realtime.MiniGames.Games.Laser;

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public enum MirrorType
{
    None,
    LeftTurn,   // 90° left
    RightTurn   // 90° right
}  

public enum LaserAxis
{
    Horizontal,
    Vertical
}

public record Position(int X, int Y);

public record Mirror(Position Position, MirrorType Type);

public record Checkpoint(Position Position);

public record LaserStep(Position Position, LaserAxis Axis);