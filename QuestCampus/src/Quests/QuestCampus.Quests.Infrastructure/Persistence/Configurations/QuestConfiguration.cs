using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuestCampus.Quests.Domain.Entities;

namespace QuestCampus.Quests.Infrastructure.Persistence.Configurations;

internal sealed class QuestConfiguration : IEntityTypeConfiguration<Quest>
{
    public void Configure(EntityTypeBuilder<Quest> builder)
    {
        builder.ToTable("Quests");
        builder.HasKey(q => q.Id);
        // Les identifiants sont générés par le domaine (Guid.NewGuid), jamais par la base.
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.Title).HasMaxLength(Quest.TitleMaxLength).IsRequired();
        builder.Property(q => q.Description).HasMaxLength(Quest.DescriptionMaxLength).IsRequired();
        builder.Property(q => q.OrganizerId).IsRequired();
        builder.HasIndex(q => q.OrganizerId);
        builder.Ignore(q => q.TotalPoints);

        builder.HasMany(q => q.Checkpoints)
            .WithOne()
            .HasForeignKey(c => c.QuestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CheckpointConfiguration : IEntityTypeConfiguration<Checkpoint>
{
    public void Configure(EntityTypeBuilder<Checkpoint> builder)
    {
        builder.ToTable("Checkpoints");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Title).HasMaxLength(Checkpoint.TitleMaxLength).IsRequired();
        builder.Property(c => c.Riddle).HasMaxLength(Checkpoint.RiddleMaxLength).IsRequired();
        builder.Property(c => c.Answer).HasMaxLength(Checkpoint.AnswerMaxLength).IsRequired();
        builder.Ignore(c => c.Location);
        builder.HasIndex(c => new { c.QuestId, c.Order }).IsUnique();
    }
}
