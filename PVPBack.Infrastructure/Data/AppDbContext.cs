using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVPBack.Core.Interfaces;
using PVPBack.Domain.Entities;

namespace PVPBack.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<GameSession> GameSessions { get; set; }
    public DbSet<AiEvaluationResult> AiEvaluationResults { get; set; }
}