import uuid
from datetime import UTC, datetime, timedelta

from quests.domain.leaderboard import LeaderboardTeam, TeamValidation, compute_leaderboard

A, B, C = uuid.uuid4(), uuid.uuid4(), uuid.uuid4()
CP1, CP2 = uuid.uuid4(), uuid.uuid4()
T0 = datetime(2026, 10, 12, 10, 0, tzinfo=UTC)
TEAMS = [LeaderboardTeam(A, "Alpha", 2), LeaderboardTeam(B, "Bravo", 3), LeaderboardTeam(C, "Charlie", 1)]


def test_teams_are_sorted_by_points_and_teams_without_points_are_listed():
    entries = compute_leaderboard(TEAMS, [
        TeamValidation(A, CP1, 100, T0),
        TeamValidation(B, CP1, 100, T0 + timedelta(minutes=1)),
        TeamValidation(B, CP2, 150, T0 + timedelta(minutes=2)),
    ])
    assert [e.team_name for e in entries] == ["Bravo", "Alpha", "Charlie"]
    assert entries[0].points == 250 and entries[0].validated_checkpoints == 2
    assert entries[2].points == 0 and entries[2].last_validation_at_utc is None


def test_tie_is_broken_by_the_earliest_last_validation():
    entries = compute_leaderboard(TEAMS, [
        TeamValidation(A, CP1, 100, T0 + timedelta(minutes=5)),
        TeamValidation(B, CP1, 100, T0 + timedelta(minutes=3)),
    ])
    assert entries[0].team_name == "Bravo"
    assert (entries[0].rank, entries[1].rank) == (1, 2)


def test_a_checkpoint_counts_only_once_per_team():
    entries = compute_leaderboard(TEAMS, [TeamValidation(A, CP1, 100, T0), TeamValidation(A, CP1, 100, T0 + timedelta(minutes=1))])
    alpha = next(e for e in entries if e.team_id == A)
    assert alpha.points == 100 and alpha.validated_checkpoints == 1


def test_teams_with_no_points_share_the_same_rank():
    assert all(e.rank == 1 for e in compute_leaderboard(TEAMS, []))
