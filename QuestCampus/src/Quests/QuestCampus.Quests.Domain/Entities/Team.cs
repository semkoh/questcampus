using QuestCampus.Quests.Domain.Common;

namespace QuestCampus.Quests.Domain.Entities;

/// <summary>Équipe de joueurs inscrite à une quête, rejointe grâce à un code d'invitation.</summary>
public sealed class Team
{
    public const int NameMaxLength = 60;
    public const int MaxMembers = 5;
    public const int InviteCodeLength = 6;

    private readonly List<TeamMember> _members = [];

    public Guid Id { get; private set; }
    public Guid QuestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string InviteCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    private Team() { } // EF Core

    /// <summary>Crée l'équipe ; son créateur en devient automatiquement le premier membre.</summary>
    public static Team Create(Guid questId, string name, string inviteCode, Guid creatorId, DateTime nowUtc)
    {
        if (inviteCode.Length != InviteCodeLength)
            throw new DomainException("Code d'invitation invalide.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            QuestId = questId,
            Name = Quest.RequireText(name, NameMaxLength, "Le nom d'équipe"),
            InviteCode = inviteCode,
            CreatedAtUtc = nowUtc
        };
        team.AddMember(creatorId, nowUtc);
        return team;
    }

    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public TeamMember AddMember(Guid userId, DateTime nowUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("Joueur inconnu.");
        if (HasMember(userId))
            throw new DomainException("Vous faites déjà partie de cette équipe.");
        if (_members.Count >= MaxMembers)
            throw new DomainException($"L'équipe est complète ({MaxMembers} joueurs maximum).");

        var member = new TeamMember(Id, userId, nowUtc);
        _members.Add(member);
        return member;
    }
}
