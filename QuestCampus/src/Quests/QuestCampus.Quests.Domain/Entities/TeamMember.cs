namespace QuestCampus.Quests.Domain.Entities;

/// <summary>Appartenance d'un joueur (userId venant du service Auth) à une équipe.</summary>
public sealed class TeamMember
{
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }

    private TeamMember() { } // EF Core

    internal TeamMember(Guid teamId, Guid userId, DateTime joinedAtUtc)
    {
        TeamId = teamId;
        UserId = userId;
        JoinedAtUtc = joinedAtUtc;
    }
}
