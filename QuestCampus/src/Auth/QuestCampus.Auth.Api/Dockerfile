# Build multi-étapes : le SDK compile, seule l'image runtime (plus légère) est livrée.
# Contexte de build = racine du dépôt (voir docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Auth/QuestCampus.Auth.Api/QuestCampus.Auth.Api.csproj src/Auth/QuestCampus.Auth.Api/
RUN dotnet restore src/Auth/QuestCampus.Auth.Api/QuestCampus.Auth.Api.csproj
COPY src/Auth/ src/Auth/
RUN dotnet publish src/Auth/QuestCampus.Auth.Api/QuestCampus.Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Utilisateur non-root fourni par l'image officielle ; écoute sur 8080.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuestCampus.Auth.Api.dll"]
