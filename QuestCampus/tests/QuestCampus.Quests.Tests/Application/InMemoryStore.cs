using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Application;

/// <summary>Implémentation en mémoire des ports de persistance : teste les cas d'usage sans base de données.</summary>
internal sealed class InMemoryStore : IQuestRepository, ITeamRepository, IAttemptRepository, IUnitOfWork, IInviteCodeGenerator
{
    public List<Quest> Quests { get; } = [];
    public List<Team> Teams { get; } = [];
    public List<Attempt> Attempts { get; } = [];
    public int SaveCount { get; private set; }
    private int _codeCounter;

    // IQuestRepository
    public Task<Quest?> GetWithCheckpointsAsync(Guid questId, CancellationToken ct) =>
        Task.FromResult(Quests.FirstOrDefault(q => q.Id == questId));

    public Task<IReadOnlyList<Quest>> ListVisibleAsync(UserContext user, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Quest>>(Quests
            .Where(q => user.IsOrganizer ? q.OrganizerId == user.UserId : q.IsPublished).ToList());

    public Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(IEnumerable<Guid> questIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(Quests
            .Where(q => questIds.Contains(q.Id)).ToDictionary(q => q.Id, q => q.Title));

    public void Add(Quest quest) => Quests.Add(quest);
    public void AddCheckpoint(Checkpoint checkpoint) { } // déjà présent dans l'agrégat en mémoire

    // ITeamRepository
    public Task<Team?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct) =>
        Task.FromResult(Teams.FirstOrDefault(t => t.InviteCode == inviteCode));

    public Task<Team?> GetTeamOfUserForQuestAsync(Guid userId, Guid questId, CancellationToken ct) =>
        Task.FromResult(Teams.FirstOrDefault(t => t.QuestId == questId && t.HasMember(userId)));

    public Task<IReadOnlyList<Team>> ListByQuestAsync(Guid questId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(t => t.QuestId == questId).ToList());

    public Task<IReadOnlyList<Team>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Team>>(Teams.Where(t => t.HasMember(userId)).ToList());

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken ct) =>
        Task.FromResult(Teams.Any(t => t.InviteCode == inviteCode));

    public void Add(Team team) => Teams.Add(team);
    public void AddMember(TeamMember member) { }

    // IAttemptRepository
    public Task<bool> HasSuccessAsync(Guid teamId, Guid checkpointId, CancellationToken ct) =>
        Task.FromResult(Attempts.Any(a => a.TeamId == teamId && a.CheckpointId == checkpointId && a.IsSuccess));

    public Task<IReadOnlyList<Guid>> ListValidatedCheckpointIdsAsync(Guid teamId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Attempts.Where(a => a.TeamId == teamId && a.IsSuccess)
            .Select(a => a.CheckpointId).ToList());

    public Task<IReadOnlyList<TeamValidation>> ListSuccessfulForQuestAsync(Guid questId, CancellationToken ct)
    {
        var checkpoints = Quests.Where(q => q.Id == questId).SelectMany(q => q.Checkpoints).ToDictionary(c => c.Id);
        return Task.FromResult<IReadOnlyList<TeamValidation>>(Attempts
            .Where(a => a.IsSuccess && checkpoints.ContainsKey(a.CheckpointId))
            .Select(a => new TeamValidation(a.TeamId, a.CheckpointId, checkpoints[a.CheckpointId].Points, a.SubmittedAtUtc))
            .ToList());
    }

    public void Add(Attempt attempt) => Attempts.Add(attempt);

    // IUnitOfWork
    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    // IInviteCodeGenerator : codes déterministes pour les tests
    public string Generate() => $"TEST{++_codeCounter:00}";
}

internal sealed class FixedClock(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
}
