namespace QuestCampus.Quests.Domain.Rules;

/// <summary>Validation réussie d'un checkpoint par une équipe (donnée d'entrée du classement).</summary>
public sealed record TeamValidation(Guid TeamId, Guid CheckpointId, int Points, DateTime ValidatedAtUtc);

/// <summary>Équipe participante (donnée d'entrée du classement).</summary>
public sealed record LeaderboardTeam(Guid TeamId, string TeamName, int MemberCount);

public sealed record LeaderboardEntry(
    int Rank,
    Guid TeamId,
    string TeamName,
    int MemberCount,
    int Points,
    int ValidatedCheckpoints,
    DateTime? LastValidationAtUtc);

/// <summary>
/// Calcul du classement : total des points décroissant ; à égalité, l'équipe
/// qui a atteint ce total le plus tôt passe devant ; puis ordre alphabétique.
/// </summary>
public static class LeaderboardCalculator
{
    public static IReadOnlyList<LeaderboardEntry> Compute(
        IEnumerable<LeaderboardTeam> teams,
        IEnumerable<TeamValidation> validations)
    {
        // Une même équipe ne marque qu'une fois par checkpoint (protection supplémentaire).
        var byTeam = validations
            .GroupBy(v => (v.TeamId, v.CheckpointId))
            .Select(g => g.OrderBy(v => v.ValidatedAtUtc).First())
            .GroupBy(v => v.TeamId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var ordered = teams
            .Select(team =>
            {
                var list = byTeam.GetValueOrDefault(team.TeamId) ?? [];
                return new
                {
                    Team = team,
                    Points = list.Sum(v => v.Points),
                    Count = list.Count,
                    Last = list.Count == 0 ? (DateTime?)null : list.Max(v => v.ValidatedAtUtc)
                };
            })
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.Last ?? DateTime.MaxValue)
            .ThenBy(x => x.Team.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<LeaderboardEntry>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var x = ordered[i];
            // Égalité parfaite (mêmes points, même instant) => même rang.
            var rank = i > 0 && ordered[i - 1].Points == x.Points && ordered[i - 1].Last == x.Last
                ? result[i - 1].Rank
                : i + 1;
            result.Add(new LeaderboardEntry(rank, x.Team.TeamId, x.Team.TeamName,
                x.Team.MemberCount, x.Points, x.Count, x.Last));
        }

        return result;
    }
}
