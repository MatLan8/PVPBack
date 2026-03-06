using PVPBack.Core.Realtime;

namespace PVPBack.Core.Interfaces;

public interface ISessionManager
{
    GameSessionRuntime Create(string sessionCode, Guid dbSessionId);
    bool TryGet(string sessionCode, out GameSessionRuntime? session);
    IReadOnlyCollection<GameSessionRuntime> GetAll();
    void Remove(string sessionCode);
}