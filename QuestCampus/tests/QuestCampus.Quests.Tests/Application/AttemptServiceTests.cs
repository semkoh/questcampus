using QuestCampus.Quests.Application.Attempts;
using QuestCampus.Quests.Application.Common;
using QuestCampus.Quests.Application.Leaderboards;
using QuestCampus.Quests.Application.Teams;
using QuestCampus.Quests.Domain.Common;
using QuestCampus.Quests.Domain.Entities;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Tests.Application;

/// <summary>Cas d'usage "soumettre une réponse" de bout en bout, avec des dépôts en mémoire.</summary>
public class AttemptServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly GeoPoint Spot = new(48.85646, 2.35242);

    private readonly InMemoryStore _store = new();
    private readonly FixedClock _clock = new(Now);
    private readonly UserContext _organizer = new(Guid.NewGuid(), IsOrganizer: true);
    private readonly UserContext _alice = new(Guid.NewGuid(), IsOrganizer: false);
    private readonly UserContext _bilal = new(Guid.NewGuid(), IsOrganizer: false);
    private readonly Quest _quest;
    private readonly Checkpoint _checkpoint;

    public AttemptServiceTests()
    {
        _quest = Quest.Create(_organizer.UserId, "Rallye", null, Now);
        _checkpoint = _quest.AddCheckpoint("Parvis", "Quel bâtiment ?", Spot, "Hôtel de Ville", 100);
        _quest.Publish();
        _store.Quests.Add(_quest);
    }

    private AttemptService Attempts() => new(_store, _store, _store, _store, _clock);
    private TeamService Teams() => new(_store, _store, _store, _store, _clock);

    private async Task<TeamDto> AliceCreatesTeamAsync() =>
        await Teams().CreateAsync(_quest.Id, new CreateTeamRequest("Alpha"), _alice, CancellationToken.None);

    private Task<AttemptResultDto> SubmitAsync(UserContext user, string answer, GeoPoint position) =>
        Attempts().SubmitAsync(_quest.Id, _checkpoint.Id,
            new SubmitAttemptRequest(answer, position.Latitude, position.Longitude), user, CancellationToken.None);

    [Fact]
    public async Task Good_answer_at_the_right_place_awards_the_points()
    {
        await AliceCreatesTeamAsync();

        var result = await SubmitAsync(_alice, "hotel de ville", Spot);

        Assert.True(result.IsValidated);
        Assert.Equal(100, result.PointsAwarded);
        Assert.Single(_store.Attempts);
        Assert.True(_store.Attempts[0].IsSuccess);
    }

    [Fact]
    public async Task Good_answer_too_far_is_recorded_as_a_failed_attempt()
    {
        await AliceCreatesTeamAsync();

        var result = await SubmitAsync(_alice, "Hôtel de Ville", new GeoPoint(48.8575, 2.3524)); // ≈ 115 m

        Assert.False(result.IsValidated);
        Assert.True(result.IsAnswerCorrect);
        Assert.False(result.IsWithinRadius);
        Assert.Equal(0, result.PointsAwarded);
        Assert.Contains("rapprochez-vous", result.Message);
        Assert.False(_store.Attempts[0].IsSuccess);
    }

    [Fact]
    public async Task A_teammate_cannot_score_the_same_checkpoint_twice()
    {
        var team = await AliceCreatesTeamAsync();
        await Teams().JoinAsync(new JoinTeamRequest(team.InviteCode.ToLowerInvariant()), _bilal, CancellationToken.None);
        await SubmitAsync(_alice, "Hôtel de Ville", Spot);

        var second = await SubmitAsync(_bilal, "Hôtel de Ville", Spot);

        Assert.True(second.AlreadyValidated);
        Assert.False(second.IsValidated);
        Assert.Equal(0, second.PointsAwarded);
        Assert.Single(_store.Attempts); // aucune nouvelle tentative enregistrée
    }

    [Fact]
    public async Task A_player_without_team_cannot_play()
    {
        await Assert.ThrowsAsync<ConflictException>(() => SubmitAsync(_alice, "Hôtel de Ville", Spot));
    }

    [Fact]
    public async Task An_organizer_cannot_submit_attempts()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => SubmitAsync(_organizer, "Hôtel de Ville", Spot));
    }

    [Fact]
    public async Task An_unknown_checkpoint_is_reported_as_not_found()
    {
        await AliceCreatesTeamAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => Attempts().SubmitAsync(_quest.Id, Guid.NewGuid(),
            new SubmitAttemptRequest("x", Spot.Latitude, Spot.Longitude), _alice, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_coordinates_are_rejected_by_the_domain()
    {
        await AliceCreatesTeamAsync();

        await Assert.ThrowsAsync<DomainException>(() => Attempts().SubmitAsync(_quest.Id, _checkpoint.Id,
            new SubmitAttemptRequest("x", 120, 2), _alice, CancellationToken.None));
    }

    [Fact]
    public async Task A_player_can_join_only_one_team_per_quest()
    {
        await AliceCreatesTeamAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            Teams().CreateAsync(_quest.Id, new CreateTeamRequest("Bis"), _alice, CancellationToken.None));
    }

    [Fact]
    public async Task Leaderboard_reflects_validated_checkpoints()
    {
        await AliceCreatesTeamAsync();
        await Teams().CreateAsync(_quest.Id, new CreateTeamRequest("Bravo"), _bilal, CancellationToken.None);
        await SubmitAsync(_alice, "Hôtel de Ville", Spot);

        var board = await new LeaderboardService(_store, _store, _store, _clock)
            .GetAsync(_quest.Id, _bilal, CancellationToken.None);

        Assert.Equal(2, board.Entries.Count);
        Assert.Equal("Alpha", board.Entries[0].TeamName);
        Assert.Equal(100, board.Entries[0].Points);
        Assert.Equal(100, board.MaxPoints);
    }
}
