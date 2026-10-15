using System.Threading.RateLimiting;
using QuestCampus.Bff.Auth;
using QuestCampus.Bff.Clients;
using QuestCampus.Bff.Endpoints;
using QuestCampus.Bff.Hubs;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBffAuthentication(builder.Configuration);

// Clients HTTP typés vers les deux services internes.
builder.Services.AddHttpClient<AuthApiClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:AuthApi"] ?? "http://localhost:5001");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<QuestsApiClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:QuestsApi"] ?? "http://localhost:5002");
    c.Timeout = TimeSpan.FromSeconds(10);
});

// YARP : routes /api/quests/** et /api/teams/** relayées vers l'API Métier.
// Le JWT (lu dans le cookie) est injecté en en-tête Authorization ; les cookies ne quittent jamais le BFF.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transforms => transforms.AddRequestTransform(transform =>
    {
        transform.ProxyRequest.Headers.Remove("Cookie");
        var token = AuthCookies.GetAccessToken(transform.HttpContext);
        if (token is not null)
            transform.ProxyRequest.Headers.Authorization = new("Bearer", token);
        return ValueTask.CompletedTask;
    }));

builder.Services.AddSignalR();

// Anti-force brute sur les réponses : 10 tentatives par minute et par joueur.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(GameEndpoints.AttemptsRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DownstreamExceptionHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapBffAuthEndpoints();
app.MapGameEndpoints();
app.MapHub<LeaderboardHub>(LeaderboardHub.Path);
app.MapReverseProxy();

app.Run();
