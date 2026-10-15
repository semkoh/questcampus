using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Infrastructure.Persistence.Repositories;

internal sealed class QuestRepository(QuestsDbContext db) : IQuestRepository
{
    public Task<Quest?> GetWithCheckpointsAsync(Guid questId, CancellationToken ct) =>
        db.Quests.Include(q => q.Checkpoints).FirstOrDefaultAsync(q => q.Id == questId, ct);

    public async Task<IReadOnlyList<Quest>> ListVisibleAsync(UserContext user, CancellationToken ct)
    {
        var query = db.Quests.AsNoTracking().Include(q => q.Checkpoints).AsQueryable();
        query = user.IsOrganizer
            ? query.Where(q => q.OrganizerId == user.UserId)
            : query.Where(q => q.IsPublished);
        return await query.OrderByDescending(q => q.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(IEnumerable<Guid> questIds, CancellationToken ct)
    {
        var ids = questIds.ToList();
        return await db.Quests.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.Title, ct);
    }

    public void Add(Quest quest) => db.Quests.Add(quest);

    public void AddCheckpoint(Checkpoint checkpoint) => db.Checkpoints.Add(checkpoint);
}

internal sealed class TeamRepository(QuestsDbContext db) : ITeamRepository
{
    public Task<Team?> GetByInviteCodeAsync(string inviteCode, CancellationToken ct) =>
        db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.InviteCode == inviteCode, ct);

    public Task<Team?> GetTeamOfUserForQuestAsync(Guid userId, Guid questId, CancellationToken ct) =>
        db.Teams.Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.QuestId == questId && t.Members.Any(m => m.UserId == userId), ct);

    public async Task<IReadOnlyList<Team>> ListByQuestAsync(Guid questId, CancellationToken ct) =>
        await db.Teams.AsNoTracking().Include(t => t.Members)
            .Where(t => t.QuestId == questId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Team>> ListForUserAsync(Guid userId, CancellationToken ct) =>
        await db.Teams.AsNoTracking().Include(t => t.Members)
            .Where(t => t.Members.Any(m => m.UserId == userId))
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken ct) =>
        db.Teams.AnyAsync(t => t.InviteCode == inviteCode, ct);

    public void Add(Team team) => db.Teams.Add(team);

    public void AddMember(TeamMember member) => db.TeamMembers.Add(member);
}

internal sealed class AttemptRepository(QuestsDbContext db) : IAttemptRepository
{
    public Task<bool> HasSuccessAsync(Guid teamId, Guid checkpointId, CancellationToken ct) =>
        db.Attempts.AnyAsync(a => a.TeamId == teamId && a.CheckpointId == checkpointId && a.IsSuccess, ct);

    public async Task<IReadOnlyList<Guid>> ListValidatedCheckpointIdsAsync(Guid teamId, CancellationToken ct) =>
        await db.Attempts.AsNoTracking()
            .Where(a => a.TeamId == teamId && a.IsSuccess)
            .Select(a => a.CheckpointId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeamValidation>> ListSuccessfulForQuestAsync(Guid questId, CancellationToken ct) =>
        await (from a in db.Attempts.AsNoTracking()
               join c in db.Checkpoints.AsNoTracking() on a.CheckpointId equals c.Id
               where c.QuestId == questId && a.IsSuccess
               select new TeamValidation(a.TeamId, a.CheckpointId, c.Points, a.SubmittedAtUtc))
            .ToListAsync(ct);

    public void Add(Attempt attempt) => db.Attempts.Add(attempt);
}

internal sealed class UnitOfWork(QuestsDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Course entre deux requêtes simultanées (ex. deux coéquipiers valident le même checkpoint).
            throw new ConflictException("Cette action vient d'être effectuée par quelqu'un d'autre. Rafraîchissez la page.");
        }
    }
}
