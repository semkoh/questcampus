"""Implémentation PostgreSQL des ports de la couche Application."""
import secrets
import uuid
from collections.abc import Iterable
from datetime import UTC, datetime

from sqlalchemy import exists, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..application.errors import ConflictError
from ..application.ports import UserContext
from ..domain.entities import INVITE_CODE_LENGTH, Attempt, Checkpoint, Quest, Team, TeamMember
from ..domain.leaderboard import TeamValidation
from .orm import (AttemptRow, CheckpointRow, QuestRow, TeamMemberRow, TeamRow, attempt_to_row, checkpoint_to_row,
                  member_to_row, quest_to_domain, quest_to_row, team_to_domain, team_to_row)


class SqlQuestRepository:
    def __init__(self, session: Session):
        self.session = session

    def get(self, quest_id: uuid.UUID) -> Quest | None:
        row = self.session.get(QuestRow, quest_id)
        return quest_to_domain(row) if row else None

    def list_visible(self, user: UserContext) -> list[Quest]:
        query = select(QuestRow).order_by(QuestRow.created_at.desc())
        query = query.where(QuestRow.organizer_id == user.user_id) if user.is_organizer \
            else query.where(QuestRow.is_published.is_(True))
        return [quest_to_domain(r) for r in self.session.scalars(query)]

    def titles(self, quest_ids: Iterable[uuid.UUID]) -> dict[uuid.UUID, str]:
        ids = list(quest_ids)
        if not ids:
            return {}
        rows = self.session.execute(select(QuestRow.id, QuestRow.title).where(QuestRow.id.in_(ids)))
        return {quest_id: title for quest_id, title in rows}

    def add(self, quest: Quest) -> None:
        self.session.add(quest_to_row(quest))

    def add_checkpoint(self, checkpoint: Checkpoint) -> None:
        self.session.add(checkpoint_to_row(checkpoint))

    def update(self, quest: Quest) -> None:
        row = self.session.get(QuestRow, quest.id)
        row.title, row.description, row.is_published = quest.title, quest.description, quest.is_published


class SqlTeamRepository:
    def __init__(self, session: Session):
        self.session = session

    def get_by_invite_code(self, invite_code: str) -> Team | None:
        row = self.session.scalar(select(TeamRow).where(TeamRow.invite_code == invite_code))
        return team_to_domain(row) if row else None

    def get_team_of_user(self, user_id: uuid.UUID, quest_id: uuid.UUID) -> Team | None:
        row = self.session.scalar(select(TeamRow).where(
            TeamRow.quest_id == quest_id, TeamRow.members.any(TeamMemberRow.user_id == user_id)))
        return team_to_domain(row) if row else None

    def list_by_quest(self, quest_id: uuid.UUID) -> list[Team]:
        return [team_to_domain(r) for r in self.session.scalars(select(TeamRow).where(TeamRow.quest_id == quest_id))]

    def list_for_user(self, user_id: uuid.UUID) -> list[Team]:
        query = (select(TeamRow).where(TeamRow.members.any(TeamMemberRow.user_id == user_id))
                 .order_by(TeamRow.created_at.desc()))
        return [team_to_domain(r) for r in self.session.scalars(query)]

    def invite_code_exists(self, invite_code: str) -> bool:
        return bool(self.session.scalar(select(exists().where(TeamRow.invite_code == invite_code))))

    def add(self, team: Team) -> None:
        self.session.add(team_to_row(team))

    def add_member(self, member: TeamMember) -> None:
        self.session.add(member_to_row(member))


class SqlAttemptRepository:
    def __init__(self, session: Session):
        self.session = session

    def has_success(self, team_id: uuid.UUID, checkpoint_id: uuid.UUID) -> bool:
        return bool(self.session.scalar(select(exists().where(
            AttemptRow.team_id == team_id, AttemptRow.checkpoint_id == checkpoint_id, AttemptRow.is_success))))

    def validated_checkpoint_ids(self, team_id: uuid.UUID) -> list[uuid.UUID]:
        return list(self.session.scalars(select(AttemptRow.checkpoint_id).where(
            AttemptRow.team_id == team_id, AttemptRow.is_success)))

    def successful_for_quest(self, quest_id: uuid.UUID) -> list[TeamValidation]:
        rows = self.session.execute(
            select(AttemptRow.team_id, AttemptRow.checkpoint_id, CheckpointRow.points, AttemptRow.submitted_at)
            .join(CheckpointRow, CheckpointRow.id == AttemptRow.checkpoint_id)
            .where(CheckpointRow.quest_id == quest_id, AttemptRow.is_success))
        return [TeamValidation(*row) for row in rows]

    def add(self, attempt: Attempt) -> None:
        self.session.add(attempt_to_row(attempt))


class SqlUnitOfWork:
    def __init__(self, session: Session):
        self.session = session

    def commit(self) -> None:
        try:
            self.session.commit()
        except IntegrityError:
            # Course entre deux requêtes simultanées (ex. deux coéquipiers valident le même checkpoint).
            self.session.rollback()
            raise ConflictError("Cette action vient d'être effectuée par quelqu'un d'autre. Rafraîchissez la page.") from None


class SecureInviteCodeGenerator:
    """6 caractères tirés avec un générateur cryptographique, sans caractères ambigus (0/O, 1/I/L)."""
    ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"

    def generate(self) -> str:
        return "".join(secrets.choice(self.ALPHABET) for _ in range(INVITE_CODE_LENGTH))


class SystemClock:
    def now(self) -> datetime:
        return datetime.now(UTC)
