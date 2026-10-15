"""Contrats JSON exposés (camelCase). Construits à partir des DTO de la couche Application."""
import uuid
from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, from_attributes=True)


# ----- Requêtes -----
class CreateQuestRequest(CamelModel):
    title: str
    description: str | None = None


class AddCheckpointRequest(CamelModel):
    title: str
    riddle: str
    latitude: float
    longitude: float
    answer: str
    points: int


class CreateTeamRequest(CamelModel):
    name: str


class JoinTeamRequest(CamelModel):
    invite_code: str


class SubmitAttemptRequest(CamelModel):
    answer: str = Field(max_length=500)
    latitude: float
    longitude: float


# ----- Réponses -----
class QuestSummaryOut(CamelModel):
    id: uuid.UUID
    title: str
    description: str
    is_published: bool
    checkpoint_count: int
    total_points: int
    is_owner: bool


class CheckpointOut(CamelModel):
    id: uuid.UUID
    order: int
    title: str
    riddle: str
    points: int
    latitude: float | None
    longitude: float | None
    answer: str | None
    is_validated_by_my_team: bool


class MyTeamOut(CamelModel):
    id: uuid.UUID
    name: str
    invite_code: str
    member_count: int


class QuestDetailOut(CamelModel):
    id: uuid.UUID
    title: str
    description: str
    is_published: bool
    is_owner: bool
    total_points: int
    checkpoints: list[CheckpointOut]
    my_team: MyTeamOut | None


class TeamOut(CamelModel):
    id: uuid.UUID
    quest_id: uuid.UUID
    quest_title: str
    name: str
    invite_code: str
    member_count: int


class AttemptResultOut(CamelModel):
    is_validated: bool
    already_validated: bool
    is_answer_correct: bool
    is_within_radius: bool
    distance_meters: float
    points_awarded: int
    message: str


class LeaderboardEntryOut(CamelModel):
    rank: int
    team_id: uuid.UUID
    team_name: str
    member_count: int
    points: int
    validated_checkpoints: int
    last_validation_at_utc: datetime | None


class LeaderboardOut(CamelModel):
    quest_id: uuid.UUID
    quest_title: str
    max_points: int
    generated_at_utc: datetime
    entries: list[LeaderboardEntryOut]
