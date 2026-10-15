using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Domain;

public class LeaderboardCalculatorTests
{
    private static readonly Guid TeamA = Guid.NewGuid();
    private static readonly Guid TeamB = Guid.NewGuid();
    private static readonly Guid TeamC = Guid.NewGuid();
    private static readonly Guid Cp1 = Guid.NewGuid();
    private static readonly Guid Cp2 = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    private static readonly LeaderboardTeam[] Teams =
    [
        new(TeamA, "Alpha", 2), new(TeamB, "Bravo", 3), new(TeamC, "Charlie", 1)
    ];

    [Fact]
    public void Teams_are_sorted_by_points_and_teams_without_points_are_listed()
    {
        var entries = LeaderboardCalculator.Compute(Teams,
        [
            new(TeamA, Cp1, 100, T0),
            new(TeamB, Cp1, 100, T0.AddMinutes(1)),
            new(TeamB, Cp2, 150, T0.AddMinutes(2))
        ]);

        Assert.Equal(3, entries.Count);
        Assert.Equal("Bravo", entries[0].TeamName);
        Assert.Equal(250, entries[0].Points);
        Assert.Equal(2, entries[0].ValidatedCheckpoints);
        Assert.Equal("Alpha", entries[1].TeamName);
        Assert.Equal("Charlie", entries[2].TeamName);
        Assert.Equal(0, entries[2].Points);
        Assert.False(entries[2].LastValidationAtUtc.HasValue);
    }

    [Fact]
    public void Tie_is_broken_by_the_earliest_last_validation()
    {
        var entries = LeaderboardCalculator.Compute(Teams,
        [
            new(TeamA, Cp1, 100, T0.AddMinutes(5)),
            new(TeamB, Cp1, 100, T0.AddMinutes(3))
        ]);

        Assert.Equal("Bravo", entries[0].TeamName);
        Assert.Equal(1, entries[0].Rank);
        Assert.Equal(2, entries[1].Rank);
    }

    [Fact]
    public void A_checkpoint_counts_only_once_per_team()
    {
        var entries = LeaderboardCalculator.Compute(Teams,
        [
            new(TeamA, Cp1, 100, T0),
            new(TeamA, Cp1, 100, T0.AddMinutes(1))
        ]);

        var alpha = entries.Single(e => e.TeamId == TeamA);
        Assert.Equal(100, alpha.Points);
        Assert.Equal(1, alpha.ValidatedCheckpoints);
    }

    [Fact]
    public void Teams_with_no_points_share_the_same_rank()
    {
        var entries = LeaderboardCalculator.Compute(Teams, []);

        Assert.All(entries, e => Assert.Equal(1, e.Rank));
    }
}
