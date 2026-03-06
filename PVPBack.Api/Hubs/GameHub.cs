using Microsoft.AspNetCore.SignalR;
using PVPBack.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace PVPBack.Hubs;

public class GameHub : Hub
{
    private readonly ISessionManager _sessionManager;

    public GameHub(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task JoinSession(string sessionCode, string nickname)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        var player = session.AddPlayer(Context.ConnectionId, nickname);

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);

        await Clients.Caller.SendAsync("ReceivePrivateData", player.PrivateData);
        await Clients.Group(sessionCode).SendAsync("ReceivePublicState", session.GetPublicState());
        await Clients.Group(sessionCode).SendAsync("WaitingRoomPlayersUpdated", new
        {
            sessionCode,
            players = session.Players.Select(p => p.Nickname).ToList()
        });
    }

    public async Task SendChat(string sessionCode, string nickname, string message)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        session.AddChat(nickname, message);

        await Clients.Group(sessionCode).SendAsync("ReceiveChat", new
        {
            nickname,
            message,
            sentAtUtc = DateTime.UtcNow
        });
    }

    public async Task SubmitAction(string sessionCode, string actionType, string payload)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        var result = session.SubmitAction(Context.ConnectionId, actionType, payload);

        await Clients.Caller.SendAsync("ActionAcknowledged", new
        {
            result.Success,
            result.Message
        });

        await Clients.Group(sessionCode).SendAsync("ReceivePublicState", session.GetPublicState());

        if (session.CurrentGame.IsCompleted)
        {
            await Clients.Group(sessionCode).SendAsync("GameCompleted", new
            {
                sessionCode
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var session in _sessionManager.GetAll())
        {
            var removed = session.RemovePlayer(Context.ConnectionId);
            if (removed)
            {
                await Clients.Group(session.SessionCode).SendAsync("WaitingRoomPlayersUpdated", new
                {
                    sessionCode = session.SessionCode,
                    players = session.Players.Select(p => p.Nickname).ToList()
                });

                await Clients.Group(session.SessionCode)
                    .SendAsync("PlayerLeft", Context.ConnectionId);

                break;
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}