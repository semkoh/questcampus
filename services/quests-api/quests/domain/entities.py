"""Entités du domaine. Les fabriques (create, record) garantissent les invariants."""
import uuid
from dataclasses import dataclass, field
from datetime import datetime

from .answers import normalize_answer
from .errors import DomainError
from .geo import GeoPoint
from .validation import ValidationResult, evaluate_attempt

QUEST_TITLE_MAX = 120
QUEST_DESCRIPTION_MAX = 2000
CHECKPOINT_TITLE_MAX = 120
RIDDLE_MAX = 1000
ANSWER_MAX = 200
MAX_POINTS = 1000
TEAM_NAME_MAX = 60
TEAM_MAX_MEMBERS = 5
INVITE_CODE_LENGTH = 6


def require_text(value: str | None, max_length: int, label: str) -> str:
    text = (value or "").strip()
    if not text:
        raise DomainError(f"{label} est obligatoire.")
    if len(text) > max_length:
        raise DomainError(f"{label} dépasse {max_length} caractères.")
    return text


@dataclass
class Checkpoint:
    """Étape d'une quête : une énigme à résoudre à un endroit précis."""
    id: uuid.UUID
    quest_id: uuid.UUID
    order: int
    title: str
    riddle: str
    latitude: float
    longitude: float
    answer: str
    points: int

    @property
    def location(self) -> GeoPoint:
        return GeoPoint(self.latitude, self.longitude)

    def evaluate(self, submitted_answer: str, player_position: GeoPoint) -> ValidationResult:
        """Applique la règle métier de validation à une tentative."""
        return evaluate_attempt(self.location, self.answer, player_position, submitted_answer)


@dataclass
class Quest:
    """Quête créée par un organisateur ; agrégat racine de ses checkpoints."""
    id: uuid.UUID
    title: str
    description: str
    organizer_id: uuid.UUID  # identifiant issu du JWT : aucune autre donnée d'authentification ici
    is_published: bool
    created_at: datetime
    checkpoints: list[Checkpoint] = field(default_factory=list)

    @classmethod
    def create(cls, organizer_id: uuid.UUID, title: str, description: str | None, now: datetime) -> "Quest":
        description = (description or "").strip()
        if len(description) > QUEST_DESCRIPTION_MAX:
            raise DomainError(f"La description dépasse {QUEST_DESCRIPTION_MAX} caractères.")
        return cls(uuid.uuid4(), require_text(title, QUEST_TITLE_MAX, "Le titre"), description,
                   organizer_id, False, now)

    @property
    def total_points(self) -> int:
        return sum(c.points for c in self.checkpoints)

    def is_owned_by(self, user_id: uuid.UUID) -> bool:
        return self.organizer_id == user_id

    def add_checkpoint(self, title: str, riddle: str, location: GeoPoint, answer: str, points: int) -> Checkpoint:
        if self.is_published:
            raise DomainError("Impossible d'ajouter un checkpoint à une quête déjà publiée.")
        if not 1 <= points <= MAX_POINTS:
            raise DomainError(f"Les points doivent être compris entre 1 et {MAX_POINTS}.")
        if not normalize_answer(answer):
            raise DomainError("La réponse attendue doit contenir au moins une lettre ou un chiffre.")
        checkpoint = Checkpoint(
            id=uuid.uuid4(), quest_id=self.id,
            order=max((c.order for c in self.checkpoints), default=0) + 1,
            title=require_text(title, CHECKPOINT_TITLE_MAX, "Le titre du checkpoint"),
            riddle=require_text(riddle, RIDDLE_MAX, "L'énigme"),
            latitude=location.latitude, longitude=location.longitude,
            answer=require_text(answer, ANSWER_MAX, "La réponse"),
            points=points,
        )
        self.checkpoints.append(checkpoint)
        return checkpoint

    def publish(self) -> None:
        if self.is_published:
            raise DomainError("La quête est déjà publiée.")
        if not self.checkpoints:
            raise DomainError("Une quête doit contenir au moins un checkpoint pour être publiée.")
        self.is_published = True


@dataclass
class TeamMember:
    team_id: uuid.UUID
    user_id: uuid.UUID
    joined_at: datetime


@dataclass
class Team:
    """Équipe de joueurs inscrite à une quête, rejointe grâce à un code d'invitation."""
    id: uuid.UUID
    quest_id: uuid.UUID
    name: str
    invite_code: str
    created_at: datetime
    members: list[TeamMember] = field(default_factory=list)

    @classmethod
    def create(cls, quest_id: uuid.UUID, name: str, invite_code: str, creator_id: uuid.UUID, now: datetime) -> "Team":
        """Le créateur devient automatiquement le premier membre."""
        if len(invite_code) != INVITE_CODE_LENGTH:
            raise DomainError("Code d'invitation invalide.")
        team = cls(uuid.uuid4(), quest_id, require_text(name, TEAM_NAME_MAX, "Le nom d'équipe"), invite_code, now)
        team.add_member(creator_id, now)
        return team

    def has_member(self, user_id: uuid.UUID) -> bool:
        return any(m.user_id == user_id for m in self.members)

    def add_member(self, user_id: uuid.UUID, now: datetime) -> TeamMember:
        if self.has_member(user_id):
            raise DomainError("Vous faites déjà partie de cette équipe.")
        if len(self.members) >= TEAM_MAX_MEMBERS:
            raise DomainError(f"L'équipe est complète ({TEAM_MAX_MEMBERS} joueurs maximum).")
        member = TeamMember(self.id, user_id, now)
        self.members.append(member)
        return member


@dataclass
class Attempt:
    """Trace d'une tentative (réussie ou non) : sert à l'audit et au calcul du classement."""
    id: uuid.UUID
    team_id: uuid.UUID
    checkpoint_id: uuid.UUID
    user_id: uuid.UUID
    submitted_answer: str
    latitude: float
    longitude: float
    distance_meters: float
    is_success: bool
    submitted_at: datetime

    @classmethod
    def record(cls, team_id: uuid.UUID, checkpoint_id: uuid.UUID, user_id: uuid.UUID, submitted_answer: str,
               position: GeoPoint, result: ValidationResult, now: datetime) -> "Attempt":
        return cls(uuid.uuid4(), team_id, checkpoint_id, user_id, (submitted_answer or "").strip()[:ANSWER_MAX],
                   position.latitude, position.longitude, result.distance_meters, result.is_validated, now)
