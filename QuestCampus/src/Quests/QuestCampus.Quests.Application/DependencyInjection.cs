using Microsoft.Extensions.DependencyInjection;
using QuestCampus.Quests.Application.Attempts;
using QuestCampus.Quests.Application.Leaderboards;
using QuestCampus.Quests.Application.Quests;
using QuestCampus.Quests.Application.Teams;

namespace QuestCampus.Quests.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<QuestService>();
        services.AddScoped<TeamService>();
        services.AddScoped<AttemptService>();
        services.AddScoped<LeaderboardService>();
        return services;
    }
}
