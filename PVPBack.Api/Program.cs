using MediatR;
using Microsoft.EntityFrameworkCore;
using PVPBack.Api;
using PVPBack.Core.Interfaces;
using PVPBack.Core.Realtime;
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
builder.Services.AddScoped<ISessionEvaluationPromptBuilder, SessionEvaluationPromptBuilder>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddScoped<SessionService>();
builder.Services.AddSingleton<GameSessionTimerSupport>();
builder.Services.AddHostedService<GameSessionTimerHostedService>();

//Ai API
var mistralConfig = builder.Configuration.GetSection("Mistral");

builder.Services.AddHttpClient<IMistralService, MistralService>(client =>
{
    client.BaseAddress = new Uri(mistralConfig["BaseUrl"] ?? "https://api.mistral.ai/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    // Saugus rakto perdavimas per Headerį
    client.DefaultRequestHeaders.Authorization = 
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mistralConfig["ApiKey"]);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "https://PVPFront.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddOpenApi();


var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/openapi/v1.json", "PVPBack"));

app.UseCors();
 

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");



app.MapGet("/health", () => Results.Ok("healthy"));
app.Run();
