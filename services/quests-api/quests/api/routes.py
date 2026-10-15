import uuid

from fastapi import APIRouter, Depends, Response

from ..application.ports import UserContext
from ..application.services import AttemptService, LeaderboardService, QuestService, TeamService
from . import schemas as s
from .auth import current_user, require_organizer, require_player
from .dependencies import attempt_service, leaderboard_service, quest_service, team_service

router = APIRouter(prefix="/api")


# ---------- Quêtes ----------
@router.get("/quests", response_model=list[s.QuestSummaryOut])
def list_quests(user: UserContext = Depends(current_user), service: QuestService = Depends(quest_service)):
    return service.list(user)


@router.get("/quests/{quest_id}", response_model=s.QuestDetailOut)
def get_quest(quest_id: uuid.UUID, user: UserContext = Depends(current_user),
              service: QuestService = Depends(quest_service)):
    return service.get(quest_id, user)


@router.post("/quests", status_code=201, response_model=s.QuestSummaryOut)
def create_quest(body: s.CreateQuestRequest, user: UserContext = Depends(require_organizer),
                 service: QuestService = Depends(quest_service)):
    return service.create(body.title, body.description, user)


@router.post("/quests/{quest_id}/checkpoints", status_code=201, response_model=s.CheckpointOut)
def add_checkpoint(quest_id: uuid.UUID, body: s.AddCheckpointRequest, user: UserContext = Depends(require_organizer),
                   service: QuestService = Depends(quest_service)):
    return service.add_checkpoint(quest_id, body.title, body.riddle, body.latitude, body.longitude,
                                  body.answer, body.points, user)


@router.post("/quests/{quest_id}/publish", status_code=204)
def publish_quest(quest_id: uuid.UUID, user: UserContext = Depends(require_organizer),
                  service: QuestService = Depends(quest_service)):
    service.publish(quest_id, user)
    return Response(status_code=204)


@router.post("/quests/{quest_id}/checkpoints/{checkpoint_id}/attempts", response_model=s.AttemptResultOut)
def submit_attempt(quest_id: uuid.UUID, checkpoint_id: uuid.UUID, body: s.SubmitAttemptRequest,
                   user: UserContext = Depends(require_player), service: AttemptService = Depends(attempt_service)):
    return service.submit(quest_id, checkpoint_id, body.answer, body.latitude, body.longitude, user)


@router.get("/quests/{quest_id}/leaderboard", response_model=s.LeaderboardOut)
def leaderboard(quest_id: uuid.UUID, user: UserContext = Depends(current_user),
                service: LeaderboardService = Depends(leaderboard_service)):
    return service.get(quest_id, user)


# ---------- Équipes ----------
@router.post("/quests/{quest_id}/teams", status_code=201, response_model=s.TeamOut)
def create_team(quest_id: uuid.UUID, body: s.CreateTeamRequest, user: UserContext = Depends(require_player),
                service: TeamService = Depends(team_service)):
    return service.create(quest_id, body.name, user)


@router.post("/teams/join", response_model=s.TeamOut)
def join_team(body: s.JoinTeamRequest, user: UserContext = Depends(require_player),
              service: TeamService = Depends(team_service)):
    return service.join(body.invite_code, user)


@router.get("/teams/mine", response_model=list[s.TeamOut])
def my_teams(user: UserContext = Depends(current_user), service: TeamService = Depends(team_service)):
    return service.list_mine(user)
