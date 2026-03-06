using PVPBack.Domain.Entities;

namespace PVPBack.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Users.Any())
            return;

        db.Users.AddRange(
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "leader1@example.com",
                Password = "a",
                DisplayName = "Leader One",
                RemainingCredits = 10
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "leader2@example.com",
                Password = "a",
                DisplayName = "Leader Two",
                RemainingCredits = 5
            });

        await db.SaveChangesAsync();
    }
}