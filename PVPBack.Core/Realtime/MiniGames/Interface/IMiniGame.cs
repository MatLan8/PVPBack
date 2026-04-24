namespace PVPBack.Core.Realtime.MiniGames;

public interface IMiniGame
{
    void Start(List<PlayerRuntime> players);
    GameActionResult SubmitAction(PlayerRuntime player, GameAction action);
    void RefreshPlayerPrivateData(List<PlayerRuntime> players);
    object GetPublicState();
    bool IsCompleted { get; }
    bool IsFailed { get; }
    
    /// <summary>
    /// Called when a player disconnects. Return their placed cards to prevent soft-lock.
    /// </summary>
    void OnPlayerDisconnect(string playerId);
}