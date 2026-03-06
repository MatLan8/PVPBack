using System.Collections.Concurrent;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;

namespace PVPBack.Infrastructure.Realtime;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, GameSessionRuntime> _sessions = new();

    public GameSessionRuntime Create(string sessionCode, Guid dbSessionId)
    {
        var session = new GameSessionRuntime(sessionCode, dbSessionId);
        _sessions[sessionCode] = session;
        return session;
    }

    public bool TryGet(string sessionCode, out GameSessionRuntime? session)
    {
        return _sessions.TryGetValue(sessionCode, out session);
    }

    public IReadOnlyCollection<GameSessionRuntime> GetAll()
    {
        return _sessions.Values.ToList().AsReadOnly();
    }

    public void Remove(string sessionCode)
    {
        _sessions.TryRemove(sessionCode, out _);
    }
}