using Microsoft.EntityFrameworkCore;
using QuestCampus.Quests.Domain.Entities;

namespace QuestCampus.Quests.Infrastructure.Persistence;

/// <summary>Contexte EF Core de la base métier (quests_db). Aucune table d'authentification ici.</summary>
public sealed class QuestsDbContext(DbContextOptions<QuestsDbContext> options) : DbContext(options)
{
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Attempt> Attempts => Set<Attempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestsDbContext).Assembly);
}
