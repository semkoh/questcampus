using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Application.Abstractions;

// Ports de persistance : implémentés par la couche Infrastructure (EF Core / PostgreSQL).

public interface IQuestRepository
{
    Task<Quest?> GetWithCheckpointsAsync(Guid questId, CancellationToken ct);
    Task<IReadOnlyList<Quest>> ListVisibleAsync(UserContext user, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(IEnumerable<Guid> questIds, CancellationToken ct);
    void Add(Quest quest);
    void AddCheckpoint(Checkpoint checkpoint);
}

public interface ITeamRepository
{
    Task<Team?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct);
    Task<Team?> GetTeamOfUserForQuestAsync(Guid userId, Guid questId, CancellationToken ct);
    Task<IReadOnlyList<Team>> ListByQuestAsync(Guid questId, CancellationToken ct);
    Task<IReadOnlyList<Team>> ListForUserAsync(Guid userId, CancellationToken ct);
    Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken ct);
    void Add(Team team);
    void AddMember(TeamMember member);
}

public interface IAttemptRepository
{
    Task<bool> HasSuccessAsync(Guid teamId, Guid checkpointId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListValidatedCheckpointIdsAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<TeamValidation>> ListSuccessfulForQuestAsync(Guid questId, CancellationToken ct);
    void Add(Attempt attempt);
}

public interface IUnitOfWork
{
    /// <summary>Persiste les changements. Lève ConflictException en cas de violation d'unicité concurrente.</summary>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IInviteCodeGenerator
{
    string Generate();
}
