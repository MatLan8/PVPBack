using System.Text.Json;

namespace PVPBack.Core.Realtime.MiniGames;

public class GameAction
{
    public string Type { get; set; } = null!;
    public JsonElement? Data { get; set; }
}