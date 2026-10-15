using System.Security.Claims;
using QuestCampus.Quests.Api.Infrastructure;
using QuestCampus.Quests.Application.Attempts;
using QuestCampus.Quests.Application.Leaderboards;
using QuestCampus.Quests.Application.Quests;

namespace QuestCampus.Quests.Api.Endpoints;

public static class QuestEndpoints
{
    public static IEndpointRouteBuilder MapQuestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/quests").RequireAuthorization();

        group.MapGet("/", (QuestService service, ClaimsPrincipal user, CancellationToken ct) =>
            service.ListAsync(user.ToUserContext(), ct));

        group.MapGet("/{questId:guid}", (Guid questId, QuestService service, ClaimsPrincipal user, CancellationToken ct) =>
            service.GetAsync(questId, user.ToUserContext(), ct));

        group.MapPost("/", async (CreateQuestRequest request, QuestService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var quest = await service.CreateAsync(request, user.ToUserContext(), ct);
                return Results.Created($"/api/quests/{quest.Id}", quest);
            })
            .RequireAuthorization(Roles.Organizer);

        group.MapPost("/{questId:guid}/checkpoints", async (Guid questId, AddCheckpointRequest request,
                QuestService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var checkpoint = await service.AddCheckpointAsync(questId, request, user.ToUserContext(), ct);
                return Results.Created($"/api/quests/{questId}", checkpoint);
            })
            .RequireAuthorization(Roles.Organizer);

        group.MapPost("/{questId:guid}/publish", async (Guid questId, QuestService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                await service.PublishAsync(questId, user.ToUserContext(), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Roles.Organizer);

        group.MapPost("/{questId:guid}/checkpoints/{checkpointId:guid}/attempts", (Guid questId, Guid checkpointId,
                SubmitAttemptRequest request, AttemptService service, ClaimsPrincipal user, CancellationToken ct) =>
            service.SubmitAsync(questId, checkpointId, request, user.ToUserContext(), ct))
            .RequireAuthorization(Roles.Player);

        group.MapGet("/{questId:guid}/leaderboard", (Guid questId, LeaderboardService service, ClaimsPrincipal user, CancellationToken ct) =>
            service.GetAsync(questId, user.ToUserContext(), ct));

        return app;
    }
}
