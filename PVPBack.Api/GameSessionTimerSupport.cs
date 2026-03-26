using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;
using PVPBack.Core.Services;
using PVPBack.Hubs;

namespace PVPBack.Api;

public class GameSessionTimerSupport
{
    private readonly ConcurrentDictionary<string, SessionTimerState> _timers = new();
    private readonly TimeSpan _duration = TimeSpan.FromMinutes(10);

    public bool StartSession(GameSessionRuntime session)
    {
        if (!session.HasStarted) return false;

        return _timers.TryAdd(session.SessionCode, new SessionTimerState
        {
            StartedAtUtc = DateTime.UtcNow,
            EndsAtUtc = DateTime.UtcNow.Add(_duration)
        });
    }

    public bool IsExpired(string sessionCode)
    {
        return _timers.TryGetValue(sessionCode, out var state)
               && !state.IsCompleted
               && DateTime.UtcNow >= state.EndsAtUtc;
    }

    public int GetRemainingSeconds(string sessionCode)
    {
        if (!_timers.TryGetValue(sessionCode, out var state))
            return 0;

        var remaining = state.EndsAtUtc - DateTime.UtcNow;
        return (int)Math.Max(0, Math.Ceiling(remaining.TotalSeconds));
    }

    public bool TryGetTimerInfo(string sessionCode, out DateTime startedAtUtc, out DateTime endsAtUtc, out int remainingSeconds)
    {
        startedAtUtc = default;
        endsAtUtc = default;
        remainingSeconds = 0;

        if (!_timers.TryGetValue(sessionCode, out var state))
            return false;

        startedAtUtc = state.StartedAtUtc;
        endsAtUtc = state.EndsAtUtc;
        remainingSeconds = GetRemainingSeconds(sessionCode);
        return true;
    }

    public void StopSession(string sessionCode)
    {
        _timers.TryRemove(sessionCode, out _);
    }

    public bool TryMarkCompleted(string sessionCode)
    {
        if (!_timers.TryGetValue(sessionCode, out var state))
            return false;

        lock (state.Sync)
        {
            if (state.IsCompleted) return false;
            state.IsCompleted = true;
            return true;
        }
    }

    public async Task<bool> TryCompleteTimedOutSessionAsync(
        GameSessionRuntime session,
        SessionService sessionService,
        IHubContext<GameHub> hubContext,
        CancellationToken cancellationToken = default)
    {
        if (!IsExpired(session.SessionCode)) return false;
        if (!TryMarkCompleted(session.SessionCode)) return false;

        await hubContext.Clients.Group(session.SessionCode).SendAsync("GameTimedOut", new
        {
            sessionCode = session.SessionCode
        }, cancellationToken);

        await sessionService.CompleteSessionAsync(session.SessionCode, cancellationToken);
        StopSession(session.SessionCode);

        return true;
    }

    private sealed class SessionTimerState
    {
        public object Sync { get; } = new();
        public DateTime StartedAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; }
        public bool IsCompleted { get; set; }
    }
}

public sealed class GameSessionTimerHostedService : BackgroundService
{
    private readonly ISessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly GameSessionTimerSupport _timerSupport;

    public GameSessionTimerHostedService(
        ISessionManager sessionManager,
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hubContext,
        GameSessionTimerSupport timerSupport)
    {
        _sessionManager = sessionManager;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _timerSupport = timerSupport;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var session in _sessionManager.GetAll())
            {
                // ✅ NORMAL GAME COMPLETION
                if (session.IsCompleted && session.TryFinalize())
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();

                    await sessionService.CompleteSessionAsync(session.SessionCode, stoppingToken);

                    _timerSupport.StopSession(session.SessionCode);

                    continue;
                }

                // ✅ TIMER UPDATES
                if (session.HasStarted &&
                    session.CurrentGame is not null &&
                    !session.CurrentGame.IsCompleted &&
                    !session.CurrentGame.IsFailed &&
                    _timerSupport.TryGetTimerInfo(
                        session.SessionCode,
                        out var startedAtUtc,
                        out var endsAtUtc,
                        out var remainingSeconds))
                {
                    await _hubContext.Clients.Group(session.SessionCode).SendAsync(
                        "TimerUpdated",
                        new
                        {
                            sessionCode = session.SessionCode,
                            remainingSeconds,
                            timerStartedAtUtc = startedAtUtc,
                            timerEndsAtUtc = endsAtUtc
                        },
                        stoppingToken);
                }

                // ✅ TIMEOUT COMPLETION
                if (!_timerSupport.IsExpired(session.SessionCode))
                    continue;

                using var timeoutScope = _scopeFactory.CreateScope();
                var sessionServiceTimeout = timeoutScope.ServiceProvider.GetRequiredService<SessionService>();

                await _timerSupport.TryCompleteTimedOutSessionAsync(
                    session,
                    sessionServiceTimeout,
                    _hubContext,
                    stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}