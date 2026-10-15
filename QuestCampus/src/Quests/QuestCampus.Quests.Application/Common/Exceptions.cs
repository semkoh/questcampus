namespace QuestCampus.Quests.Application.Common;

/// <summary>Ressource introuvable (traduit en HTTP 404 par l'API).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Action interdite pour cet utilisateur (HTTP 403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>Conflit avec l'état actuel (HTTP 409), ex. déjà membre d'une équipe.</summary>
public sealed class ConflictException(string message) : Exception(message);
