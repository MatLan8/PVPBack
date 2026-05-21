using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PVPBack.Core.Exceptions;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;
using PVPBack.Domain.Entities;
using PVPBack.Domain.Dtos;

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

    public async Task<string> CompleteSessionAsync(
        string sessionCode,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionManager.TryGet(sessionCode, out var runtimeSession) || runtimeSession is null)
            throw new InvalidOperationException("Active runtime session not found.");

        await CloseSessionInDbAsync(sessionCode, cancellationToken);
        try
        {
            return await SaveAiEvaluationAsync(sessionCode, runtimeSession, cancellationToken);
        }
        finally
        {
            _sessionManager.Remove(sessionCode);
        }
    }

    private async Task CloseSessionInDbAsync(
        string sessionCode,
        CancellationToken cancellationToken)
    {
        var dbSession = await _db.GameSessions
            .FirstOrDefaultAsync(x => x.SessionCode == sessionCode, cancellationToken);

        if (dbSession is null)
            throw new InvalidOperationException("Database session not found.");

        if (dbSession.CompletedAtUtc is not null)
            return; // already closed — idempotent

        dbSession.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> SaveAiEvaluationAsync(
        string sessionCode,
        GameSessionRuntime runtimeSession,
        CancellationToken cancellationToken)
    {
        var dbSession = await _db.GameSessions
            .FirstOrDefaultAsync(x => x.SessionCode == sessionCode, cancellationToken);

        if (dbSession is null)
            throw new InvalidOperationException("Database session not found.");

        // optional: skip if report already exists (retry-safe)
        var hasReport = await _db.AiEvaluationResults
            .AnyAsync(r => r.GameSessionId == dbSession.Id, cancellationToken);

        if (hasReport)
            return (await _db.AiEvaluationResults
                .Where(r => r.GameSessionId == dbSession.Id)
                .Select(r => r.Summary)
                .FirstAsync(cancellationToken));

        var (summary, rawJson) = await _aiEvaluationService.EvaluateAsync(runtimeSession, cancellationToken);

        _db.AiEvaluationResults.Add(new AiEvaluationResult
        {
            Id = Guid.NewGuid(),
            GameSessionId = dbSession.Id,
            Summary = summary,
            RawJson = rawJson,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
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
            throw new SessionNotFoundException(sessionCode);

        var aiResult = await _db.AiEvaluationResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameSessionId == dbSession.Id, cancellationToken);

        if (aiResult is null)
        {
            if (dbSession.CompletedAtUtc is not null)
                throw new SessionReportPendingException(sessionCode);

            throw new SessionReportPendingException(sessionCode);
        }

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

    public async Task<List<LeaderSessionResponseDto>> GetLeaderSessionsAsync(
        Guid leaderId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _db.GameSessions
            .AsNoTracking()
            .Where(s => s.LeaderId == leaderId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new LeaderSessionResponseDto
            {
                SessionId = s.Id,
                SessionCode = s.SessionCode,
                CreatedAtUtc = s.CreatedAtUtc,
                CompletedAtUtc = s.CompletedAtUtc,

                ReportId = _db.AiEvaluationResults
                    .Where(r => r.GameSessionId == s.Id)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return sessions;
    }
}

public class SessionReportResult
{
    public string SessionCode { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public JsonElement Report { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}