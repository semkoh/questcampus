import uuid
from dataclasses import dataclass
from datetime import datetime


@dataclass(frozen=True)
class TeamValidation:
    """Validation réussie d'un checkpoint par une équipe (donnée d'entrée du classement)."""
    team_id: uuid.UUID
    checkpoint_id: uuid.UUID
    points: int
    validated_at: datetime


@dataclass(frozen=True)
class LeaderboardTeam:
    team_id: uuid.UUID
    team_name: str
    member_count: int


@dataclass(frozen=True)
class LeaderboardEntry:
    rank: int
    team_id: uuid.UUID
    team_name: str
    member_count: int
    points: int
    validated_checkpoints: int
    last_validation_at_utc: datetime | None


def compute_leaderboard(teams: list[LeaderboardTeam], validations: list[TeamValidation]) -> list[LeaderboardEntry]:
    """
    Classement : total des points décroissant ; à égalité, l'équipe qui a atteint ce total le plus tôt
    passe devant ; puis ordre alphabétique. Un checkpoint ne compte qu'une fois par équipe.
    """
    first_by_pair: dict[tuple[uuid.UUID, uuid.UUID], TeamValidation] = {}
    for v in sorted(validations, key=lambda v: v.validated_at):
        first_by_pair.setdefault((v.team_id, v.checkpoint_id), v)

    by_team: dict[uuid.UUID, list[TeamValidation]] = {}
    for v in first_by_pair.values():
        by_team.setdefault(v.team_id, []).append(v)

    rows = []
    for team in teams:
        own = by_team.get(team.team_id, [])
        last = max((v.validated_at for v in own), default=None)
        rows.append((team, sum(v.points for v in own), len(own), last))

    rows.sort(key=lambda r: (-r[1], r[3] is None, r[3] or datetime.max, r[0].team_name.lower()))

    entries: list[LeaderboardEntry] = []
    for i, (team, points, count, last) in enumerate(rows):
        same_as_previous = i > 0 and rows[i - 1][1] == points and rows[i - 1][3] == last
        rank = entries[i - 1].rank if same_as_previous else i + 1
        entries.append(LeaderboardEntry(rank, team.team_id, team.team_name, team.member_count, points, count, last))
    return entries
