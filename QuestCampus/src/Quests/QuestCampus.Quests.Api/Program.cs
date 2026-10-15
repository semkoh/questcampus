using QuestCampus.Quests.Api.Endpoints;
using QuestCampus.Quests.Api.Infrastructure;
using QuestCampus.Quests.Application;
using QuestCampus.Quests.Infrastructure;
using QuestCampus.Quests.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QuestsDb")
    ?? throw new InvalidOperationException("ConnectionStrings:QuestsDb manquant.");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapQuestEndpoints();
app.MapTeamEndpoints();

// Création du schéma + données de démonstration.
using (var scope = app.Services.CreateScope())
{
    var seed = new SeedOptions(
        builder.Configuration.GetValue<Guid>("Seed:OrganizerId"),
        builder.Configuration.GetValue<Guid>("Seed:RivalPlayerId"));
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(seed, CancellationToken.None);
}

app.Run();
