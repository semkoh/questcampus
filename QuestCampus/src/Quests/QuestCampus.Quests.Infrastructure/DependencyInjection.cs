using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestCampus.Quests.Application.Abstractions;
using QuestCampus.Quests.Infrastructure.Persistence;
using QuestCampus.Quests.Infrastructure.Persistence.Repositories;
using QuestCampus.Quests.Infrastructure.Services;

namespace QuestCampus.Quests.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<QuestsDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IQuestRepository, QuestRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IAttemptRepository, AttemptRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IInviteCodeGenerator, InviteCodeGenerator>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
