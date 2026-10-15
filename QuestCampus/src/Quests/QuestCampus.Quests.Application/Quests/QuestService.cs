using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Application.Quests;

/// <summary>Cas d'usage autour des quêtes et de leurs checkpoints.</summary>
public sealed class QuestService(
    IQuestRepository quests,
    ITeamRepository teams,
    IAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<QuestSummaryDto>> ListAsync(UserContext user, CancellationToken ct)
    {
        var list = await quests.ListVisibleAsync(user, ct);
        return list
            .Select(q => new QuestSummaryDto(q.Id, q.Title, q.Description, q.IsPublished,
                q.Checkpoints.Count, q.TotalPoints, q.IsOwnedBy(user.UserId)))
            .ToList();
    }

    public async Task<QuestDetailDto> GetAsync(Guid questId, UserContext user, CancellationToken ct)
    {
        var quest = await GetVisibleQuestAsync(questId, user, ct);
        var isOwner = quest.IsOwnedBy(user.UserId);

        var team = isOwner ? null : await teams.GetTeamOfUserForQuestAsync(user.UserId, questId, ct);
        var validated = team is null
            ? new HashSet<Guid>()
            : (await attempts.ListValidatedCheckpointIdsAsync(team.Id, ct)).ToHashSet();

        var checkpoints = quest.Checkpoints
            .OrderBy(c => c.Order)
            .Select(c => new CheckpointDto(
                c.Id, c.Order, c.Title, c.Riddle, c.Points,
                isOwner ? c.Latitude : null,
                isOwner ? c.Longitude : null,
                isOwner ? c.Answer : null,
                validated.Contains(c.Id)))
            .ToList();

        var myTeam = team is null ? null : new MyTeamDto(team.Id, team.Name, team.InviteCode, team.Members.Count);

        return new QuestDetailDto(quest.Id, quest.Title, quest.Description, quest.IsPublished,
            isOwner, quest.TotalPoints, checkpoints, myTeam);
    }

    public async Task<QuestSummaryDto> CreateAsync(CreateQuestRequest request, UserContext user, CancellationToken ct)
    {
        EnsureOrganizer(user);
        var quest = Quest.Create(user.UserId, request.Title, request.Description, clock.GetUtcNow().UtcDateTime);
        quests.Add(quest);
        await unitOfWork.SaveChangesAsync(ct);
        return new QuestSummaryDto(quest.Id, quest.Title, quest.Description, quest.IsPublished, 0, 0, true);
    }

    public async Task<CheckpointDto> AddCheckpointAsync(Guid questId, AddCheckpointRequest request,
        UserContext user, CancellationToken ct)
    {
        var quest = await GetOwnedQuestAsync(questId, user, ct);
        var checkpoint = quest.AddCheckpoint(request.Title, request.Riddle,
            new GeoPoint(request.Latitude, request.Longitude), request.Answer, request.Points);
        quests.AddCheckpoint(checkpoint);
        await unitOfWork.SaveChangesAsync(ct);

        return new CheckpointDto(checkpoint.Id, checkpoint.Order, checkpoint.Title, checkpoint.Riddle,
            checkpoint.Points, checkpoint.Latitude, checkpoint.Longitude, checkpoint.Answer, false);
    }

    public async Task PublishAsync(Guid questId, UserContext user, CancellationToken ct)
    {
        var quest = await GetOwnedQuestAsync(questId, user, ct);
        quest.Publish();
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Une quête non publiée n'est visible que de son organisateur.</summary>
    internal static async Task<Quest> GetVisibleQuestAsync(IQuestRepository quests, Guid questId,
        UserContext user, CancellationToken ct)
    {
        var quest = await quests.GetWithCheckpointsAsync(questId, ct);
        if (quest is null || (!quest.IsPublished && !quest.IsOwnedBy(user.UserId)))
            throw new NotFoundException("Quête introuvable.");
        return quest;
    }

    private Task<Quest> GetVisibleQuestAsync(Guid questId, UserContext user, CancellationToken ct) =>
        GetVisibleQuestAsync(quests, questId, user, ct);

    private async Task<Quest> GetOwnedQuestAsync(Guid questId, UserContext user, CancellationToken ct)
    {
        EnsureOrganizer(user);
        var quest = await quests.GetWithCheckpointsAsync(questId, ct)
                    ?? throw new NotFoundException("Quête introuvable.");
        if (!quest.IsOwnedBy(user.UserId))
            throw new ForbiddenException("Seul l'organisateur de cette quête peut la modifier.");
        return quest;
    }

    private static void EnsureOrganizer(UserContext user)
    {
        if (!user.IsOrganizer)
            throw new ForbiddenException("Action réservée aux organisateurs.");
    }
}
