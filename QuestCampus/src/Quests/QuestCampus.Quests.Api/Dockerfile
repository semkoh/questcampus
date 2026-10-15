# Build multi-étapes des 4 projets Clean Architecture (Domain, Application, Infrastructure, Api).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Quests/QuestCampus.Quests.Domain/QuestCampus.Quests.Domain.csproj src/Quests/QuestCampus.Quests.Domain/
COPY src/Quests/QuestCampus.Quests.Application/QuestCampus.Quests.Application.csproj src/Quests/QuestCampus.Quests.Application/
COPY src/Quests/QuestCampus.Quests.Infrastructure/QuestCampus.Quests.Infrastructure.csproj src/Quests/QuestCampus.Quests.Infrastructure/
COPY src/Quests/QuestCampus.Quests.Api/QuestCampus.Quests.Api.csproj src/Quests/QuestCampus.Quests.Api/
RUN dotnet restore src/Quests/QuestCampus.Quests.Api/QuestCampus.Quests.Api.csproj
COPY src/Quests/ src/Quests/
RUN dotnet publish src/Quests/QuestCampus.Quests.Api/QuestCampus.Quests.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuestCampus.Quests.Api.dll"]
