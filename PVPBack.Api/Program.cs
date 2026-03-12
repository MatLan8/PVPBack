using MediatR;
using Microsoft.EntityFrameworkCore;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Services;
using PVPBack.Hubs;
using PVPBack.Infrastructure.Data;
using PVPBack.Infrastructure.Realtime;
using PVPBack.Infrastructure.Services;


var builder = WebApplication.CreateBuilder(args);

/*
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("BackendDB"));
builder.Services.AddScoped<IAppDbContext>(sp =>
    sp.GetRequiredService<AppDbContext>());
*/

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());


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
        policy.WithOrigins(
                "http://localhost:5173",
                "https://your-frontend-name.vercel.app"
            )
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
    app.UseHttpsRedirection();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "PVPBack")
    );
}

app.UseCors();


app.MapControllers();
app.MapHub<GameHub>("/hubs/game");



app.MapGet("/health", () => Results.Ok("healthy"));
app.Run();

