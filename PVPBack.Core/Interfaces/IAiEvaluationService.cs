using PVPBack.Core.Realtime;

namespace PVPBack.Core.Interfaces;

public interface IAiEvaluationService
{
    Task<(string Summary, string RawJson)> EvaluateAsync(
        GameSessionRuntime session,
        CancellationToken cancellationToken = default);
}