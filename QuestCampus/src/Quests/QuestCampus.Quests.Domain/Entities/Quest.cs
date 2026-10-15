using QuestCampus.Quests.Domain.Common;
using QuestCampus.Quests.Domain.Rules;

namespace QuestCampus.Quests.Domain.Entities;

/// <summary>Quête créée par un organisateur ; agrégat racine des checkpoints.</summary>
public sealed class Quest
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 2000;

    private readonly List<Checkpoint> _checkpoints = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    /// <summary>Identifiant de l'organisateur (issu du JWT). Aucune autre donnée d'authentification n'est stockée.</summary>
    public Guid OrganizerId { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Checkpoint> Checkpoints => _checkpoints.AsReadOnly();

    private Quest() { } // EF Core

    public static Quest Create(Guid organizerId, string title, string? description, DateTime nowUtc)
    {
        if (organizerId == Guid.Empty)
            throw new DomainException("Organisateur inconnu.");

        var cleanDescription = (description ?? string.Empty).Trim();
        if (cleanDescription.Length > DescriptionMaxLength)
            throw new DomainException($"La description dépasse {DescriptionMaxLength} caractères.");

        return new Quest
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = RequireText(title, TitleMaxLength, "Le titre"),
            Description = cleanDescription,
            CreatedAtUtc = nowUtc
        };
    }

    public bool IsOwnedBy(Guid userId) => OrganizerId == userId;

    public Checkpoint AddCheckpoint(string title, string riddle, GeoPoint location, string answer, int points)
    {
        if (IsPublished)
            throw new DomainException("Impossible d'ajouter un checkpoint à une quête déjà publiée.");

        var order = _checkpoints.Count == 0 ? 1 : _checkpoints.Max(c => c.Order) + 1;
        var checkpoint = Checkpoint.Create(Id, order, title, riddle, location, answer, points);
        _checkpoints.Add(checkpoint);
        return checkpoint;
    }

    public void Publish()
    {
        if (IsPublished)
            throw new DomainException("La quête est déjà publiée.");
        if (_checkpoints.Count == 0)
            throw new DomainException("Une quête doit contenir au moins un checkpoint pour être publiée.");
        IsPublished = true;
    }

    public int TotalPoints => _checkpoints.Sum(c => c.Points);

    internal static string RequireText(string? value, int maxLength, string label)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new DomainException($"{label} est obligatoire.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{label} dépasse {maxLength} caractères.");
        return trimmed;
    }
}
