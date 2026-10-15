using QuestCampus.Quests.Domain.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Domain;

public class QuestAndTeamTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly GeoPoint Somewhere = new(48.85, 2.35);

    [Fact]
    public void A_quest_without_checkpoint_cannot_be_published()
    {
        var quest = Quest.Create(Guid.NewGuid(), "Quête", null, Now);
        Assert.Throws<DomainException>(quest.Publish);
    }

    [Fact]
    public void Checkpoints_are_numbered_in_order_and_points_are_summed()
    {
        var quest = Quest.Create(Guid.NewGuid(), "Quête", null, Now);
        var first = quest.AddCheckpoint("A", "Énigme A", Somewhere, "a", 100);
        var second = quest.AddCheckpoint("B", "Énigme B", Somewhere, "b", 50);

        Assert.Equal(1, first.Order);
        Assert.Equal(2, second.Order);
        Assert.Equal(150, quest.TotalPoints);
    }

    [Fact]
    public void A_published_quest_cannot_receive_new_checkpoints()
    {
        var quest = Quest.Create(Guid.NewGuid(), "Quête", null, Now);
        quest.AddCheckpoint("A", "Énigme", Somewhere, "a", 100);
        quest.Publish();

        Assert.True(quest.IsPublished);
        Assert.Throws<DomainException>(() => quest.AddCheckpoint("B", "Énigme", Somewhere, "b", 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public void Checkpoint_points_must_be_between_1_and_1000(int points)
    {
        var quest = Quest.Create(Guid.NewGuid(), "Quête", null, Now);
        Assert.Throws<DomainException>(() => quest.AddCheckpoint("A", "Énigme", Somewhere, "a", points));
    }

    [Fact]
    public void Team_creator_is_the_first_member_and_duplicates_are_refused()
    {
        var creator = Guid.NewGuid();
        var team = Team.Create(Guid.NewGuid(), "Alpha", "ABC234", creator, Now);

        Assert.True(team.HasMember(creator));
        Assert.Throws<DomainException>(() => team.AddMember(creator, Now));
    }

    [Fact]
    public void A_team_is_limited_to_five_members()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha", "ABC234", Guid.NewGuid(), Now);
        for (var i = 1; i < Team.MaxMembers; i++)
            team.AddMember(Guid.NewGuid(), Now);

        Assert.Equal(Team.MaxMembers, team.Members.Count);
        Assert.Throws<DomainException>(() => team.AddMember(Guid.NewGuid(), Now));
    }
}
