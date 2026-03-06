namespace PVPBack.Core.Services;

using PVPBack.Core.Interfaces;
using PVPBack.Domain.Entities;
using Microsoft.EntityFrameworkCore;


public class SessionService
{
    private readonly IAppDbContext _db;
    private readonly ISessionManager _sessionManager;

    public SessionService(IAppDbContext db, ISessionManager sessionManager)
    {
        _db = db;
        _sessionManager = sessionManager;
    }

    public async Task<GameSession> StartSessionAsync(Guid leaderId, CancellationToken cancellationToken = default)
    {
        var leader = await _db.Users.FirstOrDefaultAsync(x => x.Id == leaderId, cancellationToken);
        if (leader is null)
            throw new InvalidOperationException("Leader not found.");

        if (leader.RemainingCredits <= 0)
            throw new InvalidOperationException("Leader has no remaining credits.");

        leader.RemainingCredits--;

        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            SessionCode = GenerateSessionCode(),
            LeaderId = leaderId,
            Leader = leader,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _sessionManager.Create(session.SessionCode, session.Id);

        return session;
    }

    public async Task<string> CompleteSessionAsync(
        string sessionCode,
        IAiEvaluationService aiEvaluationService,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionManager.TryGet(sessionCode, out var runtime) || runtime is null)
            throw new InvalidOperationException("Live session not found.");

        var dbSession = await _db.GameSessions
            .Include(x => x.AiEvaluationResult)
            .FirstOrDefaultAsync(x => x.SessionCode == sessionCode, cancellationToken);

        if (dbSession is null)
            throw new InvalidOperationException("Database session not found.");

        var (summary, rawJson) = await aiEvaluationService.EvaluateAsync(runtime);

        dbSession.CompletedAtUtc = DateTime.UtcNow;
        dbSession.AiEvaluationResult = new AiEvaluationResult
        {
            Id = Guid.NewGuid(),
            GameSessionId = dbSession.Id,
            GameSession = dbSession,
            Summary = summary,
            RawJson = rawJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _db.SaveChangesAsync(cancellationToken);
        _sessionManager.Remove(sessionCode);

        return summary;
    }

    public async Task<int> GetRemainingCreditsAsync(Guid leaderId, CancellationToken cancellationToken = default)
    {
        var leader = await _db.Users.FirstOrDefaultAsync(x => x.Id == leaderId, cancellationToken);
        if (leader is null)
            throw new InvalidOperationException("Leader not found.");

        return leader.RemainingCredits;
    }

    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();

        return new string(
            Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
    }
}