namespace QuestCampus.Quests.Application.Quests;

public sealed record CreateQuestRequest(string Title, string? Description);

public sealed record AddCheckpointRequest(
    string Title, string Riddle, double Latitude, double Longitude, string Answer, int Points);

public sealed record QuestSummaryDto(
    Guid Id, string Title, string Description, bool IsPublished,
    int CheckpointCount, int TotalPoints, bool IsOwner);

/// <summary>
/// Vue d'un checkpoint. Pour un joueur, la réponse et les coordonnées exactes sont masquées (null) :
/// la validation se fait exclusivement côté serveur.
/// </summary>
public sealed record CheckpointDto(
    Guid Id, int Order, string Title, string Riddle, int Points,
    double? Latitude, double? Longitude, string? Answer, bool IsValidatedByMyTeam);

public sealed record MyTeamDto(Guid Id, string Name, string InviteCode, int MemberCount);

public sealed record QuestDetailDto(
    Guid Id, string Title, string Description, bool IsPublished, bool IsOwner, int TotalPoints,
    IReadOnlyList<CheckpointDto> Checkpoints, MyTeamDto? MyTeam);
