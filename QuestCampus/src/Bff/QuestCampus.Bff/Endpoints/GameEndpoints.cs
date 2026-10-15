using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using QuestCampus.Bff.Auth;
using QuestCampus.Bff.Clients;
using QuestCampus.Bff.Hubs;

namespace QuestCampus.Bff.Endpoints;

/// <summary>Endpoints à valeur ajoutée du BFF (au-delà du simple reverse proxy YARP).</summary>
public static class GameEndpoints
{
    public const string AttemptsRateLimitPolicy = "attempts";

    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        // AGRÉGATION : un seul appel du front => 3 appels parallèles vers 2 services différents.
        app.MapGet("/bff/dashboard", async (HttpContext context, ClaimsPrincipal principal,
            AuthApiClient auth, QuestsApiClient quests, ILogger<Program> logger, CancellationToken ct) =>
        {
            var token = AuthCookies.GetAccessToken(context);

            var profileTask = auth.GetAsync("/api/auth/me", token, ct);
            var questsTask = quests.GetAsync("/api/quests", token, ct);
            var teamsTask = quests.GetAsync("/api/teams/mine", token, ct);

            var questsResponse = await questsTask;
            var teamsResponse = await teamsTask;
            if (!questsResponse.IsSuccess) return questsResponse.ToResult();
            if (!teamsResponse.IsSuccess) return teamsResponse.ToResult();

            // Mode dégradé : si le service Auth ne répond pas, on se contente des informations du JWT.
            object profile;
            try
            {
                var profileResponse = await profileTask;
                profile = profileResponse.IsSuccess ? profileResponse.Json() : BffAuthEndpoints.UserFromClaims(principal);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Service Auth indisponible, profil issu du jeton : {Message}", ex.Message);
                profile = BffAuthEndpoints.UserFromClaims(principal);
            }

            var questList = questsResponse.Json();
            var teamList = teamsResponse.Json();
            return Results.Ok(new
            {
                user = profile,
                quests = questList,
                myTeams = teamList,
                stats = new
                {
                    availableQuests = questList.GetArrayLength(),
                    myTeamCount = teamList.GetArrayLength()
                }
            });
        }).RequireAuthorization();

        // ORCHESTRATION : soumission d'une réponse puis diffusion du classement en temps réel.
        app.MapPost("/bff/quests/{questId:guid}/checkpoints/{checkpointId:guid}/attempts",
            async (Guid questId, Guid checkpointId, JsonElement attempt, HttpContext context,
                QuestsApiClient quests, IHubContext<LeaderboardHub> hub, CancellationToken ct) =>
            {
                var token = AuthCookies.GetAccessToken(context);
                var result = await quests.PostAsync(
                    $"/api/quests/{questId}/checkpoints/{checkpointId}/attempts", token, attempt, ct);

                if (result.IsSuccess && result.Json().GetProperty("isValidated").GetBoolean())
                {
                    var leaderboard = await quests.GetAsync($"/api/quests/{questId}/leaderboard", token, ct);
                    if (leaderboard.IsSuccess)
                        await hub.Clients.Group(LeaderboardHub.GroupName(questId))
                            .SendAsync(LeaderboardHub.LeaderboardUpdated, leaderboard.Json(), ct);
                }

                return result.ToResult();
            })
            .RequireAuthorization()
            .RequireRateLimiting(AttemptsRateLimitPolicy);

        return app;
    }
}
