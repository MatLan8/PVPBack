using Microsoft.EntityFrameworkCore;
using PVPBack.Domain.Entities;


namespace PVPBack.Core.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<GameSession> GameSessions { get; }
    DbSet<AiEvaluationResult> AiEvaluationResults { get; }

    DbSet<ProcessedStripeCheckout> ProcessedStripeCheckouts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}