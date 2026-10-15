"""Cas d'usage de bout en bout (Application + Domaine) avec des dépôts en mémoire."""
import pytest

from quests.application.errors import ConflictError, ForbiddenError, NotFoundError
from quests.application.services import AttemptService, LeaderboardService, QuestService, TeamService
from quests.domain.entities import Quest
from quests.domain.errors import DomainError
from quests.domain.geo import GeoPoint

from .fakes import (FakeAttempts, FakeQuests, FakeTeams, FakeUnitOfWork, FixedClock, SequentialCodes, Store,
                    new_user)

SPOT = GeoPoint(48.85646, 2.35242)


@pytest.fixture
def ctx():
    store = Store()
    organizer, alice, bilal = new_user(organizer=True), new_user(), new_user()
    quest = Quest.create(organizer.user_id, "Rallye", None, FixedClock().now())
    checkpoint = quest.add_checkpoint("Parvis", "Quel bâtiment ?", SPOT, "Hôtel de Ville", 100)
    quest.publish()
    store.quests.append(quest)

    q, t, a, u, clock = FakeQuests(store), FakeTeams(store), FakeAttempts(store), FakeUnitOfWork(store), FixedClock()

    class Ctx:
        pass
    c = Ctx()
    c.store, c.quest, c.checkpoint = store, quest, checkpoint
    c.organizer, c.alice, c.bilal = organizer, alice, bilal
    c.quests_service = QuestService(q, t, a, u, clock)
    c.teams = TeamService(q, t, SequentialCodes(), u, clock)
    c.attempts = AttemptService(q, t, a, u, clock)
    c.leaderboard = LeaderboardService(q, t, a, clock)
    c.submit = lambda user, answer, pos, cp=None: c.attempts.submit(
        quest.id, cp or checkpoint.id, answer, pos.latitude, pos.longitude, user)
    return c


def test_good_answer_at_the_right_place_awards_the_points(ctx):
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    result = ctx.submit(ctx.alice, "hotel de ville", SPOT)
    assert result.is_validated and result.points_awarded == 100
    assert len(ctx.store.attempts) == 1 and ctx.store.attempts[0].is_success


def test_good_answer_too_far_is_recorded_as_a_failed_attempt(ctx):
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    result = ctx.submit(ctx.alice, "Hôtel de Ville", GeoPoint(48.8575, 2.3524))  # ≈ 115 m
    assert not result.is_validated and result.is_answer_correct and not result.is_within_radius
    assert result.points_awarded == 0 and "rapprochez-vous" in result.message
    assert not ctx.store.attempts[0].is_success


def test_a_teammate_cannot_score_the_same_checkpoint_twice(ctx):
    team = ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    ctx.teams.join(team.invite_code.lower(), ctx.bilal)
    ctx.submit(ctx.alice, "Hôtel de Ville", SPOT)
    second = ctx.submit(ctx.bilal, "Hôtel de Ville", SPOT)
    assert second.already_validated and not second.is_validated and second.points_awarded == 0
    assert len(ctx.store.attempts) == 1


def test_a_player_without_team_cannot_play(ctx):
    with pytest.raises(ConflictError):
        ctx.submit(ctx.alice, "Hôtel de Ville", SPOT)


def test_an_organizer_cannot_submit_attempts(ctx):
    with pytest.raises(ForbiddenError):
        ctx.submit(ctx.organizer, "Hôtel de Ville", SPOT)


def test_an_unknown_checkpoint_is_not_found(ctx):
    import uuid
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    with pytest.raises(NotFoundError):
        ctx.submit(ctx.alice, "x", SPOT, cp=uuid.uuid4())


def test_invalid_coordinates_are_rejected_by_the_domain(ctx):
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    with pytest.raises(DomainError):
        ctx.attempts.submit(ctx.quest.id, ctx.checkpoint.id, "x", 120, 2, ctx.alice)


def test_a_player_can_join_only_one_team_per_quest(ctx):
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    with pytest.raises(ConflictError):
        ctx.teams.create(ctx.quest.id, "Bis", ctx.alice)


def test_an_unknown_invite_code_is_not_found(ctx):
    with pytest.raises(NotFoundError):
        ctx.teams.join("NOPE42", ctx.alice)


def test_players_never_receive_answers_or_coordinates(ctx):
    detail = ctx.quests_service.get(ctx.quest.id, ctx.alice)
    assert all(c.answer is None and c.latitude is None for c in detail.checkpoints)
    owner_view = ctx.quests_service.get(ctx.quest.id, ctx.organizer)
    assert owner_view.checkpoints[0].answer == "Hôtel de Ville"


def test_a_draft_quest_is_invisible_to_players(ctx):
    draft = ctx.quests_service.create("Brouillon", None, ctx.organizer)
    with pytest.raises(NotFoundError):
        ctx.quests_service.get(draft.id, ctx.alice)


def test_only_the_owner_can_modify_a_quest(ctx):
    other_organizer = new_user(organizer=True)
    with pytest.raises(ForbiddenError):
        ctx.quests_service.publish(ctx.quest.id, other_organizer)


def test_leaderboard_reflects_validated_checkpoints(ctx):
    ctx.teams.create(ctx.quest.id, "Alpha", ctx.alice)
    ctx.teams.create(ctx.quest.id, "Bravo", ctx.bilal)
    ctx.submit(ctx.alice, "Hôtel de Ville", SPOT)
    board = ctx.leaderboard.get(ctx.quest.id, ctx.bilal)
    assert [e.team_name for e in board.entries] == ["Alpha", "Bravo"]
    assert board.entries[0].points == 100 and board.max_points == 100
