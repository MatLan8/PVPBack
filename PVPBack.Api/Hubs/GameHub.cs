using Microsoft.AspNetCore.SignalR;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;
using PVPBack.Core.Realtime.MiniGames;

namespace PVPBack.Hubs;

public class GameHub : Hub
{
    private readonly ISessionManager _sessionManager;

    public GameHub(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    public async Task JoinSession(string sessionCode, string playerId, string nickname)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        try
        {
            var player = session.AddOrReconnectPlayer(playerId, Context.ConnectionId!, nickname);

            await Groups.AddToGroupAsync(Context.ConnectionId!, sessionCode);

            await Clients.Caller.SendAsync("ReceivePrivateData", player.PrivateData);
            await Clients.Caller.SendAsync("ReceivePublicState", session.GetPublicState());
            await Clients.Caller.SendAsync("WaitingRoomPlayersUpdated", session.GetWaitingRoomState());

            await Clients.OthersInGroup(sessionCode).SendAsync("ReceivePublicState", session.GetPublicState());
            await Clients.OthersInGroup(sessionCode).SendAsync("WaitingRoomPlayersUpdated", session.GetWaitingRoomState());

            if (session.HasStarted)
            {
                await Clients.Group(sessionCode).SendAsync("GameStarted", new
                {
                    sessionCode
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public Task<object> GetSessionState(string sessionCode)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        return Task.FromResult(session.GetPublicState());
    }

    public Task<object> GetWaitingRoomState(string sessionCode)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        return Task.FromResult(session.GetWaitingRoomState());
    }

    public Task<List<ChatMessage>> GetChatHistory(string sessionCode)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        return Task.FromResult(session.GetChatHistory());
    }

    public async Task SendChat(string sessionCode, string message)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        try
        {
            var chatMessage = session.AddChat(Context.ConnectionId!, message);

            await Clients.Group(sessionCode).SendAsync("ReceiveChat", new
            {
                playerId = chatMessage.PlayerId,
                nickname = chatMessage.Nickname,
                message = chatMessage.Message,
                sentAtUtc = chatMessage.SentAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public async Task SubmitAction(string sessionCode, GameAction action)
    {
        if (!_sessionManager.TryGet(sessionCode, out var session) || session is null)
            throw new HubException("Session not found.");

        var result = session.SubmitAction(Context.ConnectionId!, action);

        await Clients.Caller.SendAsync("ActionAcknowledged", new
        {
            result.Success,
            result.Message
        });

        await Clients.Group(sessionCode).SendAsync("ReceivePublicState", session.GetPublicState());

        foreach (var player in session.Players.Where(p => p.IsConnected && p.ConnectionId is not null))
        {
            await Clients.Client(player.ConnectionId!).SendAsync("ReceivePrivateData", player.PrivateData);
        }

        if (result.UiMessage is not null)
        {
            await Clients.Group(sessionCode).SendAsync("ReceiveGameToast", new
            {
                variant = result.UiMessage.Variant,
                message = result.UiMessage.Message
            });
        }

        if (session.CurrentGame.IsCompleted)
        {
            await Clients.Group(sessionCode).SendAsync("GameCompleted", new
            {
                sessionCode
            });
        }

        if (session.CurrentGame.IsFailed)
        {
            await Clients.Group(sessionCode).SendAsync("GameFailed", new
            {
                sessionCode
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var session in _sessionManager.GetAll())
        {
            var marked = session.MarkDisconnected(Context.ConnectionId!);
            if (marked)
            {
                await Clients.Group(session.SessionCode).SendAsync("ReceivePublicState", session.GetPublicState());
                await Clients.Group(session.SessionCode).SendAsync("WaitingRoomPlayersUpdated", session.GetWaitingRoomState());
                break;
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}