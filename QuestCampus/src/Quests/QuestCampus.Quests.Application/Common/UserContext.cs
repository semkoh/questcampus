namespace QuestCampus.Quests.Application.Common;

/// <summary>Identité de l'appelant, extraite du JWT par la couche Api.</summary>
public sealed record UserContext(Guid UserId, bool IsOrganizer);
