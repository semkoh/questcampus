"""Composition : chaque requête reçoit des cas d'usage branchés sur les adaptateurs PostgreSQL."""
from fastapi import Depends
from sqlalchemy.orm import Session

from ..application.services import AttemptService, LeaderboardService, QuestService, TeamService
from ..infrastructure.database import get_session
from ..infrastructure.repositories import (SecureInviteCodeGenerator, SqlAttemptRepository, SqlQuestRepository,
                                           SqlTeamRepository, SqlUnitOfWork, SystemClock)

_clock = SystemClock()
_codes = SecureInviteCodeGenerator()


def quest_service(s: Session = Depends(get_session)) -> QuestService:
    return QuestService(SqlQuestRepository(s), SqlTeamRepository(s), SqlAttemptRepository(s), SqlUnitOfWork(s), _clock)


def team_service(s: Session = Depends(get_session)) -> TeamService:
    return TeamService(SqlQuestRepository(s), SqlTeamRepository(s), _codes, SqlUnitOfWork(s), _clock)


def attempt_service(s: Session = Depends(get_session)) -> AttemptService:
    return AttemptService(SqlQuestRepository(s), SqlTeamRepository(s), SqlAttemptRepository(s), SqlUnitOfWork(s), _clock)


def leaderboard_service(s: Session = Depends(get_session)) -> LeaderboardService:
    return LeaderboardService(SqlQuestRepository(s), SqlTeamRepository(s), SqlAttemptRepository(s), _clock)
