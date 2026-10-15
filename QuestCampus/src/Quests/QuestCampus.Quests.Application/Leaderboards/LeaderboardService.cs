using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Application.Quests;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Application.Leaderboards;

public sealed record LeaderboardDto(
    Guid QuestId, string QuestTitle, int MaxPoints, DateTime GeneratedAtUtc,
    IReadOnlyList<LeaderboardEntry> Entries);

public sealed class LeaderboardService(
    IQuestRepository quests,
    ITeamRepository teams,
    IAttemptRepository attempts,
    TimeProvider clock)
{
    public async Task<LeaderboardDto> GetAsync(Guid questId, UserContext user, CancellationToken ct)
    {
        var quest = await QuestService.GetVisibleQuestAsync(quests, questId, user, ct);
        var questTeams = await teams.ListByQuestAsync(questId, ct);
        var validations = await attempts.ListSuccessfulForQuestAsync(questId, ct);

        var entries = LeaderboardCalculator.Compute(
            questTeams.Select(t => new LeaderboardTeam(t.Id, t.Name, t.Members.Count)),
            validations);

        return new LeaderboardDto(quest.Id, quest.Title, quest.TotalPoints, clock.GetUtcNow().UtcDateTime, entries);
    }
}
