namespace PVPBack.Core.Realtime.MiniGames.Games.Wordle;

public class WordleGame : IMiniGame
{
    private List<PlayerRuntime> _players = new()
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }
            
    private const int MaxMistakes = 3;
    private int _mistakeCount = 0;
    public void Start(List<PlayerRuntime> players)
    {
        throw new NotImplementedException();
    }

    public GameActionResult SubmitAction(PlayerRuntime player, GameAction action)
    {
        throw new NotImplementedException();
    }

    public void RefreshPlayerPrivateData(List<PlayerRuntime> players)
    {
        throw new NotImplementedException();
    }

    public object GetPublicState()
    {
        throw new NotImplementedException();
    }

    
    
    
    
}