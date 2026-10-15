import uuid
from datetime import UTC, datetime

import pytest

from quests.domain.entities import TEAM_MAX_MEMBERS, Quest, Team
from quests.domain.errors import DomainError
from quests.domain.geo import GeoPoint

NOW = datetime(2026, 10, 12, 10, 0, tzinfo=UTC)
SOMEWHERE = GeoPoint(48.85, 2.35)


def new_quest() -> Quest:
    return Quest.create(uuid.uuid4(), "Quête", None, NOW)


def test_a_quest_without_checkpoint_cannot_be_published():
    with pytest.raises(DomainError):
        new_quest().publish()


def test_checkpoints_are_numbered_and_points_are_summed():
    quest = new_quest()
    first = quest.add_checkpoint("A", "Énigme A", SOMEWHERE, "a", 100)
    second = quest.add_checkpoint("B", "Énigme B", SOMEWHERE, "b", 50)
    assert (first.order, second.order) == (1, 2)
    assert quest.total_points == 150


def test_a_published_quest_cannot_receive_new_checkpoints():
    quest = new_quest()
    quest.add_checkpoint("A", "Énigme", SOMEWHERE, "a", 100)
    quest.publish()
    with pytest.raises(DomainError):
        quest.add_checkpoint("B", "Énigme", SOMEWHERE, "b", 10)


@pytest.mark.parametrize("points", [0, -5, 1001])
def test_checkpoint_points_must_be_between_1_and_1000(points):
    with pytest.raises(DomainError):
        new_quest().add_checkpoint("A", "Énigme", SOMEWHERE, "a", points)


def test_title_is_required():
    with pytest.raises(DomainError):
        Quest.create(uuid.uuid4(), "   ", None, NOW)


def test_team_creator_is_first_member_and_duplicates_are_refused():
    creator = uuid.uuid4()
    team = Team.create(uuid.uuid4(), "Alpha", "ABC234", creator, NOW)
    assert team.has_member(creator)
    with pytest.raises(DomainError):
        team.add_member(creator, NOW)


def test_a_team_is_limited_to_five_members():
    team = Team.create(uuid.uuid4(), "Alpha", "ABC234", uuid.uuid4(), NOW)
    for _ in range(TEAM_MAX_MEMBERS - 1):
        team.add_member(uuid.uuid4(), NOW)
    with pytest.raises(DomainError):
        team.add_member(uuid.uuid4(), NOW)
