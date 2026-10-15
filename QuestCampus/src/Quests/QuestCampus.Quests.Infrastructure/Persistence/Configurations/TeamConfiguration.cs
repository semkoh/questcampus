using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuestCampus.Quests.Domain.Entities;

namespace QuestCampus.Quests.Infrastructure.Persistence.Configurations;

internal sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(t => t.InviteCode).HasMaxLength(Team.InviteCodeLength).IsRequired();
        builder.HasIndex(t => t.InviteCode).IsUnique();
        builder.HasIndex(t => t.QuestId);

        builder.HasOne<Quest>()
            .WithMany()
            .HasForeignKey(t => t.QuestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Members)
            .WithOne()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        // Clé composite : un joueur n'apparaît qu'une fois par équipe.
        builder.HasKey(m => new { m.TeamId, m.UserId });
        builder.HasIndex(m => m.UserId);
    }
}

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.SubmittedAnswer).HasMaxLength(Attempt.SubmittedAnswerMaxLength).IsRequired();

        builder.HasOne<Team>().WithMany().HasForeignKey(a => a.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Checkpoint>().WithMany().HasForeignKey(a => a.CheckpointId).OnDelete(DeleteBehavior.Cascade);

        // Garantie en base : une seule tentative RÉUSSIE par (équipe, checkpoint),
        // même si deux coéquipiers valident au même instant.
        builder.HasIndex(a => new { a.TeamId, a.CheckpointId })
            .IsUnique()
            .HasFilter("\"IsSuccess\" = TRUE")
            .HasDatabaseName("IX_Attempts_OneSuccessPerTeamAndCheckpoint");
    }
}
