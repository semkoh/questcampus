"""Tables de la base quests_db. Ces classes ne sortent jamais de l'Infrastructure (mapping vers le Domaine)."""
import uuid
from datetime import datetime

from sqlalchemy import Boolean, DateTime, Float, ForeignKey, Index, Integer, String, UniqueConstraint, Uuid, text
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship

from ..domain import entities as e


class Base(DeclarativeBase):
    pass


class QuestRow(Base):
    __tablename__ = "quests"
    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True)
    title: Mapped[str] = mapped_column(String(e.QUEST_TITLE_MAX))
    description: Mapped[str] = mapped_column(String(e.QUEST_DESCRIPTION_MAX))
    organizer_id: Mapped[uuid.UUID] = mapped_column(Uuid, index=True)  # userId issu du JWT, pas de clé étrangère
    is_published: Mapped[bool] = mapped_column(Boolean)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    checkpoints: Mapped[list["CheckpointRow"]] = relationship(
        order_by="CheckpointRow.order", cascade="all, delete-orphan", lazy="selectin")


class CheckpointRow(Base):
    __tablename__ = "checkpoints"
    __table_args__ = (UniqueConstraint("quest_id", "order", name="uq_checkpoints_quest_order"),)
    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True)
    quest_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("quests.id", ondelete="CASCADE"))
    order: Mapped[int] = mapped_column(Integer)
    title: Mapped[str] = mapped_column(String(e.CHECKPOINT_TITLE_MAX))
    riddle: Mapped[str] = mapped_column(String(e.RIDDLE_MAX))
    latitude: Mapped[float] = mapped_column(Float)
    longitude: Mapped[float] = mapped_column(Float)
    answer: Mapped[str] = mapped_column(String(e.ANSWER_MAX))
    points: Mapped[int] = mapped_column(Integer)


class TeamRow(Base):
    __tablename__ = "teams"
    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True)
    quest_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("quests.id", ondelete="CASCADE"), index=True)
    name: Mapped[str] = mapped_column(String(e.TEAM_NAME_MAX))
    invite_code: Mapped[str] = mapped_column(String(e.INVITE_CODE_LENGTH), unique=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))
    members: Mapped[list["TeamMemberRow"]] = relationship(cascade="all, delete-orphan", lazy="selectin")


class TeamMemberRow(Base):
    __tablename__ = "team_members"
    # Clé composite : un joueur n'apparaît qu'une fois par équipe.
    team_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("teams.id", ondelete="CASCADE"), primary_key=True)
    user_id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True, index=True)
    joined_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


class AttemptRow(Base):
    __tablename__ = "attempts"
    __table_args__ = (
        # Garantie en base : une seule tentative RÉUSSIE par (équipe, checkpoint),
        # même si deux coéquipiers valident au même instant.
        Index("ix_attempts_one_success_per_team_checkpoint", "team_id", "checkpoint_id",
              unique=True, postgresql_where=text("is_success")),
    )
    id: Mapped[uuid.UUID] = mapped_column(Uuid, primary_key=True)
    team_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("teams.id", ondelete="CASCADE"), index=True)
    checkpoint_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("checkpoints.id", ondelete="CASCADE"))
    user_id: Mapped[uuid.UUID] = mapped_column(Uuid)
    submitted_answer: Mapped[str] = mapped_column(String(e.ANSWER_MAX))
    latitude: Mapped[float] = mapped_column(Float)
    longitude: Mapped[float] = mapped_column(Float)
    distance_meters: Mapped[float] = mapped_column(Float)
    is_success: Mapped[bool] = mapped_column(Boolean)
    submitted_at: Mapped[datetime] = mapped_column(DateTime(timezone=True))


# ---------- Conversion lignes <-> entités du domaine ----------

def checkpoint_to_domain(r: CheckpointRow) -> e.Checkpoint:
    return e.Checkpoint(r.id, r.quest_id, r.order, r.title, r.riddle, r.latitude, r.longitude, r.answer, r.points)


def checkpoint_to_row(c: e.Checkpoint) -> CheckpointRow:
    return CheckpointRow(id=c.id, quest_id=c.quest_id, order=c.order, title=c.title, riddle=c.riddle,
                         latitude=c.latitude, longitude=c.longitude, answer=c.answer, points=c.points)


def quest_to_domain(r: QuestRow) -> e.Quest:
    return e.Quest(r.id, r.title, r.description, r.organizer_id, r.is_published, r.created_at,
                   [checkpoint_to_domain(c) for c in r.checkpoints])


def quest_to_row(q: e.Quest) -> QuestRow:
    return QuestRow(id=q.id, title=q.title, description=q.description, organizer_id=q.organizer_id,
                    is_published=q.is_published, created_at=q.created_at,
                    checkpoints=[checkpoint_to_row(c) for c in q.checkpoints])


def member_to_row(m: e.TeamMember) -> TeamMemberRow:
    return TeamMemberRow(team_id=m.team_id, user_id=m.user_id, joined_at=m.joined_at)


def team_to_domain(r: TeamRow) -> e.Team:
    return e.Team(r.id, r.quest_id, r.name, r.invite_code, r.created_at,
                  [e.TeamMember(m.team_id, m.user_id, m.joined_at) for m in r.members])


def team_to_row(t: e.Team) -> TeamRow:
    return TeamRow(id=t.id, quest_id=t.quest_id, name=t.name, invite_code=t.invite_code,
                   created_at=t.created_at, members=[member_to_row(m) for m in t.members])


def attempt_to_row(a: e.Attempt) -> AttemptRow:
    return AttemptRow(id=a.id, team_id=a.team_id, checkpoint_id=a.checkpoint_id, user_id=a.user_id,
                      submitted_answer=a.submitted_answer, latitude=a.latitude, longitude=a.longitude,
                      distance_meters=a.distance_meters, is_success=a.is_success, submitted_at=a.submitted_at)
