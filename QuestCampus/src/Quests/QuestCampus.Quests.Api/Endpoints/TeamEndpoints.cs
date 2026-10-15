using System.Security.Claims;
using QuestCampus.Quests.Api.Infrastructure;
using QuestCampus.Quests.Application.Teams;

namespace QuestCampus.Quests.Api.Endpoints;

public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/quests/{questId:guid}/teams", async (Guid questId, CreateTeamRequest request,
                TeamService service, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var team = await service.CreateAsync(questId, request, user.ToUserContext(), ct);
                return Results.Created("/api/teams/mine", team);
            })
            .RequireAuthorization(Roles.Player);

        var teams = app.MapGroup("/api/teams").RequireAuthorization();

        teams.MapPost("/join", (JoinTeamRequest request, TeamService service, ClaimsPrincipal user, CancellationToken ct) =>
                service.JoinAsync(request, user.ToUserContext(), ct))
            .RequireAuthorization(Roles.Player);

        teams.MapGet("/mine", (TeamService service, ClaimsPrincipal user, CancellationToken ct) =>
            service.ListMineAsync(user.ToUserContext(), ct));

        return app;
    }
}
