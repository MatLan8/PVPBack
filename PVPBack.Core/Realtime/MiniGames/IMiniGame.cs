namespace PVPBack.Core.Realtime.MiniGames;

public interface IMiniGame
{
    void Start(List<PlayerRuntime> players);
    GameActionResult SubmitAction(PlayerRuntime player, string actionType, string payload);
    object GetPublicState();
    bool IsCompleted { get; }
}