"""Objets renvoyés par les cas d'usage (sans dépendance au web)."""
import uuid
from dataclasses import dataclass
from datetime import datetime

from ..domain.leaderboard import LeaderboardEntry


@dataclass(frozen=True)
class QuestSummary:
    id: uuid.UUID
    title: str
    description: str
    is_published: bool
    checkpoint_count: int
    total_points: int
    is_owner: bool


@dataclass(frozen=True)
class CheckpointView:
    """Pour un joueur, la réponse et les coordonnées exactes sont masquées (None) : la validation se fait côté serveur."""
    id: uuid.UUID
    order: int
    title: str
    riddle: str
    points: int
    latitude: float | None
    longitude: float | None
    answer: str | None
    is_validated_by_my_team: bool


@dataclass(frozen=True)
class MyTeam:
    id: uuid.UUID
    name: str
    invite_code: str
    member_count: int


@dataclass(frozen=True)
class QuestDetail:
    id: uuid.UUID
    title: str
    description: str
    is_published: bool
    is_owner: bool
    total_points: int
    checkpoints: list[CheckpointView]
    my_team: MyTeam | None


@dataclass(frozen=True)
class TeamView:
    id: uuid.UUID
    quest_id: uuid.UUID
    quest_title: str
    name: str
    invite_code: str
    member_count: int


@dataclass(frozen=True)
class AttemptOutcome:
    is_validated: bool
    already_validated: bool
    is_answer_correct: bool
    is_within_radius: bool
    distance_meters: float
    points_awarded: int
    message: str


@dataclass(frozen=True)
class Leaderboard:
    quest_id: uuid.UUID
    quest_title: str
    max_points: int
    generated_at_utc: datetime
    entries: list[LeaderboardEntry]
