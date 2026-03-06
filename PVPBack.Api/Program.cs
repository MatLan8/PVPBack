using MediatR;
using Microsoft.EntityFrameworkCore;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Services;
using PVPBack.Hubs;
using PVPBack.Infrastructure.Data;
using PVPBack.Infrastructure.Realtime;
using PVPBack.Infrastructure.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BackendDB"));
builder.Services.AddScoped<IAppDbContext>(sp =>
    sp.GetRequiredService<AppDbContext>());

builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddScoped<IAiEvaluationService, AiEvaluationService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddScoped<SessionService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOpenApi();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "PVPBack")
    );
}

app.UseCors();
app.UseHttpsRedirection();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    if (db.Database.IsInMemory())
    {
        await DbSeeder.SeedAsync(db);
    }
}


app.Run();

