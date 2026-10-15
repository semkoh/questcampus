"""Cas d'usage de l'API Métier."""
import uuid

from ..domain.entities import Attempt, Quest, Team
from ..domain.geo import GeoPoint
from ..domain.leaderboard import LeaderboardTeam, compute_leaderboard
from ..domain.validation import VALIDATION_RADIUS_METERS, ValidationResult
from .dtos import AttemptOutcome, CheckpointView, Leaderboard, MyTeam, QuestDetail, QuestSummary, TeamView
from .errors import ConflictError, ForbiddenError, NotFoundError
from .ports import (AttemptRepository, Clock, InviteCodeGenerator, QuestRepository, TeamRepository, UnitOfWork,
                    UserContext)

MAX_CODE_GENERATION_ATTEMPTS = 10


def get_visible_quest(quests: QuestRepository, quest_id: uuid.UUID, user: UserContext) -> Quest:
    """Une quête non publiée n'est visible que de son organisateur."""
    quest = quests.get(quest_id)
    if quest is None or (not quest.is_published and not quest.is_owned_by(user.user_id)):
        raise NotFoundError("Quête introuvable.")
    return quest


class QuestService:
    def __init__(self, quests: QuestRepository, teams: TeamRepository, attempts: AttemptRepository,
                 uow: UnitOfWork, clock: Clock):
        self.quests, self.teams, self.attempts, self.uow, self.clock = quests, teams, attempts, uow, clock

    def list(self, user: UserContext) -> list[QuestSummary]:
        return [QuestSummary(q.id, q.title, q.description, q.is_published, len(q.checkpoints), q.total_points,
                             q.is_owned_by(user.user_id)) for q in self.quests.list_visible(user)]

    def get(self, quest_id: uuid.UUID, user: UserContext) -> QuestDetail:
        quest = get_visible_quest(self.quests, quest_id, user)
        is_owner = quest.is_owned_by(user.user_id)
        team = None if is_owner else self.teams.get_team_of_user(user.user_id, quest_id)
        validated = set(self.attempts.validated_checkpoint_ids(team.id)) if team else set()

        checkpoints = [
            CheckpointView(c.id, c.order, c.title, c.riddle, c.points,
                           c.latitude if is_owner else None,
                           c.longitude if is_owner else None,
                           c.answer if is_owner else None,
                           c.id in validated)
            for c in sorted(quest.checkpoints, key=lambda c: c.order)
        ]
        my_team = MyTeam(team.id, team.name, team.invite_code, len(team.members)) if team else None
        return QuestDetail(quest.id, quest.title, quest.description, quest.is_published, is_owner,
                           quest.total_points, checkpoints, my_team)

    def create(self, title: str, description: str | None, user: UserContext) -> QuestSummary:
        self._ensure_organizer(user)
        quest = Quest.create(user.user_id, title, description, self.clock.now())
        self.quests.add(quest)
        self.uow.commit()
        return QuestSummary(quest.id, quest.title, quest.description, False, 0, 0, True)

    def add_checkpoint(self, quest_id: uuid.UUID, title: str, riddle: str, latitude: float, longitude: float,
                       answer: str, points: int, user: UserContext) -> CheckpointView:
        quest = self._get_owned(quest_id, user)
        c = quest.add_checkpoint(title, riddle, GeoPoint(latitude, longitude), answer, points)
        self.quests.add_checkpoint(c)
        self.uow.commit()
        return CheckpointView(c.id, c.order, c.title, c.riddle, c.points, c.latitude, c.longitude, c.answer, False)

    def publish(self, quest_id: uuid.UUID, user: UserContext) -> None:
        quest = self._get_owned(quest_id, user)
        quest.publish()
        self.quests.update(quest)
        self.uow.commit()

    def _get_owned(self, quest_id: uuid.UUID, user: UserContext) -> Quest:
        self._ensure_organizer(user)
        quest = self.quests.get(quest_id)
        if quest is None:
            raise NotFoundError("Quête introuvable.")
        if not quest.is_owned_by(user.user_id):
            raise ForbiddenError("Seul l'organisateur de cette quête peut la modifier.")
        return quest

    @staticmethod
    def _ensure_organizer(user: UserContext) -> None:
        if not user.is_organizer:
            raise ForbiddenError("Action réservée aux organisateurs.")


class TeamService:
    def __init__(self, quests: QuestRepository, teams: TeamRepository, codes: InviteCodeGenerator,
                 uow: UnitOfWork, clock: Clock):
        self.quests, self.teams, self.codes, self.uow, self.clock = quests, teams, codes, uow, clock

    def create(self, quest_id: uuid.UUID, name: str, user: UserContext) -> TeamView:
        self._ensure_player(user)
        quest = get_visible_quest(self.quests, quest_id, user)
        self._ensure_not_in_team(user.user_id, quest_id)
        team = Team.create(quest.id, name, self._unique_code(), user.user_id, self.clock.now())
        self.teams.add(team)
        self.uow.commit()
        return self._view(team, quest.title)

    def join(self, invite_code: str, user: UserContext) -> TeamView:
        self._ensure_player(user)
        team = self.teams.get_by_invite_code((invite_code or "").strip().upper())
        if team is None:
            raise NotFoundError("Aucune équipe ne correspond à ce code d'invitation.")
        quest = get_visible_quest(self.quests, team.quest_id, user)
        self._ensure_not_in_team(user.user_id, team.quest_id)
        member = team.add_member(user.user_id, self.clock.now())
        self.teams.add_member(member)
        self.uow.commit()
        return self._view(team, quest.title)

    def list_mine(self, user: UserContext) -> list[TeamView]:
        mine = self.teams.list_for_user(user.user_id)
        titles = self.quests.titles({t.quest_id for t in mine})
        return [self._view(t, titles.get(t.quest_id, "Quête")) for t in mine]

    def _ensure_not_in_team(self, user_id: uuid.UUID, quest_id: uuid.UUID) -> None:
        if self.teams.get_team_of_user(user_id, quest_id) is not None:
            raise ConflictError("Vous faites déjà partie d'une équipe pour cette quête.")

    def _unique_code(self) -> str:
        for _ in range(MAX_CODE_GENERATION_ATTEMPTS):
            code = self.codes.generate()
            if not self.teams.invite_code_exists(code):
                return code
        raise ConflictError("Impossible de générer un code d'invitation unique, réessayez.")

    @staticmethod
    def _ensure_player(user: UserContext) -> None:
        if user.is_organizer:
            raise ForbiddenError("Les organisateurs ne participent pas aux quêtes.")

    @staticmethod
    def _view(team: Team, quest_title: str) -> TeamView:
        return TeamView(team.id, team.quest_id, quest_title, team.name, team.invite_code, len(team.members))


class AttemptService:
    """Cas d'usage principal : un joueur soumet une réponse depuis sa position GPS."""

    def __init__(self, quests: QuestRepository, teams: TeamRepository, attempts: AttemptRepository,
                 uow: UnitOfWork, clock: Clock):
        self.quests, self.teams, self.attempts, self.uow, self.clock = quests, teams, attempts, uow, clock

    def submit(self, quest_id: uuid.UUID, checkpoint_id: uuid.UUID, answer: str, latitude: float,
               longitude: float, user: UserContext) -> AttemptOutcome:
        if user.is_organizer:
            raise ForbiddenError("Les organisateurs ne peuvent pas valider de checkpoint.")

        quest = get_visible_quest(self.quests, quest_id, user)  # quête non publiée => 404 pour un joueur
        checkpoint = next((c for c in quest.checkpoints if c.id == checkpoint_id), None)
        if checkpoint is None:
            raise NotFoundError("Checkpoint introuvable dans cette quête.")

        team = self.teams.get_team_of_user(user.user_id, quest_id)
        if team is None:
            raise ConflictError("Rejoignez ou créez une équipe avant de jouer.")

        if self.attempts.has_success(team.id, checkpoint.id):
            return AttemptOutcome(False, True, True, True, 0, 0, "Votre équipe a déjà validé ce checkpoint.")

        position = GeoPoint(latitude, longitude)
        result = checkpoint.evaluate(answer, position)
        self.attempts.add(Attempt.record(team.id, checkpoint.id, user.user_id, answer, position, result,
                                         self.clock.now()))
        self.uow.commit()

        points = checkpoint.points if result.is_validated else 0
        return AttemptOutcome(result.is_validated, False, result.is_answer_correct, result.is_within_radius,
                              result.distance_meters, points, _message(result, points))


def _message(r: ValidationResult, points: int) -> str:
    if r.is_validated:
        return f"Checkpoint validé ! +{points} points pour votre équipe."
    if r.is_answer_correct:
        return (f"Bonne réponse, mais vous êtes à {r.distance_meters:.0f} m du point : "
                f"rapprochez-vous à moins de {VALIDATION_RADIUS_METERS:.0f} m.")
    if r.is_within_radius:
        return "Vous êtes au bon endroit, mais la réponse est incorrecte."
    return f"Mauvaise réponse et vous êtes à {r.distance_meters:.0f} m du point."


class LeaderboardService:
    def __init__(self, quests: QuestRepository, teams: TeamRepository, attempts: AttemptRepository, clock: Clock):
        self.quests, self.teams, self.attempts, self.clock = quests, teams, attempts, clock

    def get(self, quest_id: uuid.UUID, user: UserContext) -> Leaderboard:
        quest = get_visible_quest(self.quests, quest_id, user)
        teams = [LeaderboardTeam(t.id, t.name, len(t.members)) for t in self.teams.list_by_quest(quest_id)]
        entries = compute_leaderboard(teams, self.attempts.successful_for_quest(quest_id))
        return Leaderboard(quest.id, quest.title, quest.total_points, self.clock.now(), entries)
