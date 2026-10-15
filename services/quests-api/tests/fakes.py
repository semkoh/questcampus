"""Implémentations en mémoire des ports : on teste les cas d'usage sans base de données."""
import uuid
from datetime import UTC, datetime

from quests.application.ports import UserContext
from quests.domain.leaderboard import TeamValidation


class Store:
    def __init__(self):
        self.quests, self.teams, self.attempts = [], [], []
        self.commits = 0


class FakeQuests:
    def __init__(self, store: Store):
        self.s = store

    def get(self, quest_id):
        return next((q for q in self.s.quests if q.id == quest_id), None)

    def list_visible(self, user):
        return [q for q in self.s.quests if (q.organizer_id == user.user_id if user.is_organizer else q.is_published)]

    def titles(self, ids):
        ids = set(ids)
        return {q.id: q.title for q in self.s.quests if q.id in ids}

    def add(self, quest):
        self.s.quests.append(quest)

    def add_checkpoint(self, checkpoint):
        pass  # déjà présent dans l'agrégat en mémoire

    def update(self, quest):
        pass


class FakeTeams:
    def __init__(self, store: Store):
        self.s = store

    def get_by_invite_code(self, code):
        return next((t for t in self.s.teams if t.invite_code == code), None)

    def get_team_of_user(self, user_id, quest_id):
        return next((t for t in self.s.teams if t.quest_id == quest_id and t.has_member(user_id)), None)

    def list_by_quest(self, quest_id):
        return [t for t in self.s.teams if t.quest_id == quest_id]

    def list_for_user(self, user_id):
        return [t for t in self.s.teams if t.has_member(user_id)]

    def invite_code_exists(self, code):
        return any(t.invite_code == code for t in self.s.teams)

    def add(self, team):
        self.s.teams.append(team)

    def add_member(self, member):
        pass  # déjà présent dans l'agrégat en mémoire


class FakeAttempts:
    def __init__(self, store: Store):
        self.s = store

    def has_success(self, team_id, checkpoint_id):
        return any(a.team_id == team_id and a.checkpoint_id == checkpoint_id and a.is_success for a in self.s.attempts)

    def validated_checkpoint_ids(self, team_id):
        return [a.checkpoint_id for a in self.s.attempts if a.team_id == team_id and a.is_success]

    def successful_for_quest(self, quest_id):
        cps = {c.id: c for q in self.s.quests if q.id == quest_id for c in q.checkpoints}
        return [TeamValidation(a.team_id, a.checkpoint_id, cps[a.checkpoint_id].points, a.submitted_at)
                for a in self.s.attempts if a.is_success and a.checkpoint_id in cps]

    def add(self, attempt):
        self.s.attempts.append(attempt)


class FakeUnitOfWork:
    def __init__(self, store: Store):
        self.s = store

    def commit(self):
        self.s.commits += 1


class SequentialCodes:
    """Codes d'invitation déterministes pour les tests."""
    def __init__(self):
        self.n = 0

    def generate(self):
        self.n += 1
        return f"TEST{self.n:02d}"


class FixedClock:
    def now(self):
        return datetime(2026, 10, 12, 10, 0, tzinfo=UTC)


def new_user(organizer: bool = False) -> UserContext:
    return UserContext(uuid.uuid4(), organizer)
