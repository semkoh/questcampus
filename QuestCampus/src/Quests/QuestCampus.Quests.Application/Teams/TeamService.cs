using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Application.Quests;
using QuestCampus.Quests.Domain.Entities;

namespace QuestCampus.Quests.Application.Teams;

public sealed record CreateTeamRequest(string Name);
public sealed record JoinTeamRequest(string InviteCode);
public sealed record TeamDto(Guid Id, Guid QuestId, string QuestTitle, string Name, string InviteCode, int MemberCount);

/// <summary>Création d'équipe et adhésion par code d'invitation.</summary>
public sealed class TeamService(
    IQuestRepository quests,
    ITeamRepository teams,
    IInviteCodeGenerator codes,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private const int MaxCodeGenerationAttempts = 10;

    public async Task<TeamDto> CreateAsync(Guid questId, CreateTeamRequest request, UserContext user, CancellationToken ct)
    {
        EnsurePlayer(user);
        var quest = await QuestService.GetVisibleQuestAsync(quests, questId, user, ct);
        await EnsureNotInTeamAsync(user.UserId, questId, ct);

        var code = await GenerateUniqueCodeAsync(ct);
        var team = Team.Create(quest.Id, request.Name, code, user.UserId, clock.GetUtcNow().UtcDateTime);
        teams.Add(team);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(team, quest.Title);
    }

    public async Task<TeamDto> JoinAsync(JoinTeamRequest request, UserContext user, CancellationToken ct)
    {
        EnsurePlayer(user);
        var code = (request.InviteCode ?? string.Empty).Trim().ToUpperInvariant();
        var team = await teams.GetByInviteCodeAsync(code, ct)
                   ?? throw new NotFoundException("Aucune équipe ne correspond à ce code d'invitation.");

        var quest = await QuestService.GetVisibleQuestAsync(quests, team.QuestId, user, ct);
        await EnsureNotInTeamAsync(user.UserId, team.QuestId, ct);

        var member = team.AddMember(user.UserId, clock.GetUtcNow().UtcDateTime);
        teams.AddMember(member);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(team, quest.Title);
    }

    public async Task<IReadOnlyList<TeamDto>> ListMineAsync(UserContext user, CancellationToken ct)
    {
        var mine = await teams.ListForUserAsync(user.UserId, ct);
        var titles = await quests.GetTitlesAsync(mine.Select(t => t.QuestId).Distinct(), ct);
        return mine
            .Select(t => ToDto(t, titles.GetValueOrDefault(t.QuestId, "Quête")))
            .ToList();
    }

    private async Task EnsureNotInTeamAsync(Guid userId, Guid questId, CancellationToken ct)
    {
        if (await teams.GetTeamOfUserForQuestAsync(userId, questId, ct) is not null)
            throw new ConflictException("Vous faites déjà partie d'une équipe pour cette quête.");
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        for (var i = 0; i < MaxCodeGenerationAttempts; i++)
        {
            var code = codes.Generate();
            if (!await teams.InviteCodeExistsAsync(code, ct))
                return code;
        }
        throw new ConflictException("Impossible de générer un code d'invitation unique, réessayez.");
    }

    private static void EnsurePlayer(UserContext user)
    {
        if (user.IsOrganizer)
            throw new ForbiddenException("Les organisateurs ne participent pas aux quêtes.");
    }

    private static TeamDto ToDto(Team team, string questTitle) =>
        new(team.Id, team.QuestId, questTitle, team.Name, team.InviteCode, team.Members.Count);
}
