using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace QuestCampus.Bff.Hubs;

/// <summary>
/// Hub temps réel : chaque client s'abonne au groupe de la quête qu'il regarde
/// et reçoit "LeaderboardUpdated" dès qu'une équipe valide un checkpoint.
/// </summary>
[Authorize]
public sealed class LeaderboardHub : Hub
{
    public const string Path = "/hubs/leaderboard";
    public const string LeaderboardUpdated = "LeaderboardUpdated";

    public static string GroupName(Guid questId) => $"quest-{questId:N}";

    public Task JoinQuest(Guid questId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(questId));

    public Task LeaveQuest(Guid questId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(questId));
}
