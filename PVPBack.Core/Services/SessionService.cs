using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PVPBack.Core.Interfaces;
using PVPBack.Domain.Entities;

namespace PVPBack.Core.Services;

public class SessionService
{
    private readonly IAppDbContext _db;
    private readonly ISessionManager _sessionManager;
    private readonly IAiEvaluationService _aiEvaluationService;

    public SessionService(
        IAppDbContext db,
        ISessionManager sessionManager,
        IAiEvaluationService aiEvaluationService)
    {
        _db = db;
        _sessionManager = sessionManager;
        _aiEvaluationService = aiEvaluationService;
    }

    public async Task<GameSession> StartSessionAsync(Guid leaderId, CancellationToken cancellationToken = default)
    {
        var leader = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == leaderId, cancellationToken);

        if (leader is null)
            throw new InvalidOperationException("Leader not found.");

        if (leader.RemainingCredits <= 0)
            throw new InvalidOperationException("Leader has no remaining credits.");

        leader.RemainingCredits--;

        var session = new GameSession
        {
            Id = Guid.NewGuid(),
            SessionCode = await GenerateSessionCodeAsync(cancellationToken),
            LeaderId = leaderId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _sessionManager.Create(session.SessionCode, session.Id);

        return session;
    }

    public async Task<string> CompleteSessionAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        if (!_sessionManager.TryGet(sessionCode, out var runtimeSession) || runtimeSession is null)
            throw new InvalidOperationException("Active runtime session not found.");

        var dbSession = await _db.GameSessions
            .FirstOrDefaultAsync(x => x.SessionCode == sessionCode, cancellationToken);

        if (dbSession is null)
            throw new InvalidOperationException("Database session not found.");

        if (dbSession.CompletedAtUtc is not null)
            throw new InvalidOperationException("Session is already completed.");

        var (summary, rawJson) = await _aiEvaluationService.EvaluateAsync(runtimeSession, cancellationToken);

        var aiResult = new AiEvaluationResult
        {
            Id = Guid.NewGuid(),
            GameSessionId = dbSession.Id,
            Summary = summary,
            RawJson = rawJson,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbSession.CompletedAtUtc = DateTime.UtcNow;

        _db.AiEvaluationResults.Add(aiResult);
        await _db.SaveChangesAsync(cancellationToken);

        _sessionManager.Remove(sessionCode);

        return summary;
    }

    public async Task<SessionReportResult> GetSessionReportAsync(
        string sessionCode,
        CancellationToken cancellationToken = default)
    {
        var dbSession = await _db.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionCode == sessionCode, cancellationToken);

        if (dbSession is null)
            throw new InvalidOperationException("Database session not found.");

        var aiResult = await _db.AiEvaluationResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameSessionId == dbSession.Id, cancellationToken);

        if (aiResult is null)
            throw new InvalidOperationException("AI report not found for this session.");

        JsonElement reportJson;
        try
        {
            using var doc = JsonDocument.Parse(aiResult.RawJson);
            reportJson = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Stored AI report is not valid JSON.", ex);
        }

        return new SessionReportResult
        {
            SessionCode = dbSession.SessionCode,
            Summary = aiResult.Summary,
            Report = reportJson,
            CreatedAtUtc = aiResult.CreatedAtUtc
        };
    }

    public async Task<int> GetRemainingCreditsAsync(Guid leaderId, CancellationToken cancellationToken = default)
    {
        var leader = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == leaderId, cancellationToken);

        if (leader is null)
            throw new InvalidOperationException("Leader not found.");

        return leader.RemainingCredits;
    }

    private async Task<string> GenerateSessionCodeAsync(CancellationToken cancellationToken)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        const int length = 6;
        var random = new Random();

        while (true)
        {
            var code = new string(Enumerable.Range(0, length)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());

            var exists = await _db.GameSessions
                .AnyAsync(x => x.SessionCode == code, cancellationToken);

            if (!exists)
                return code;
        }
    }
}

public class SessionReportResult
{
    public string SessionCode { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public JsonElement Report { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}