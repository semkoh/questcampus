namespace QuestCampus.Quests.Domain.Common;

/// <summary>Violation d'une règle métier (invariant du domaine).</summary>
public sealed class DomainException(string message) : Exception(message);
