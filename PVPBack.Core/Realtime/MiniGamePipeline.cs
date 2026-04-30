using PVPBack.Core.Realtime.MiniGames;
using PVPBack.Core.Realtime.MiniGames.Games.Laser;


namespace PVPBack.Core.Realtime;
 
public static class MiniGamePipeline
{
    /// <summary>Ordered mini-games for a session. Extend this list when you add new IMiniGame implementations.</summary>
    public static IReadOnlyList<IMiniGame> CreateDefaultPipeline()
    {
        return new IMiniGame[]
        {
            new MiniGames.WordleGame(),
            new MiniGames.LaserGame(),
            new MiniGames.ConnectionsGame(),
            new MiniGames.TimelineGame(),
        };
    }
}