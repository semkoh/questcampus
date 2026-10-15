FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Bff/QuestCampus.Bff/QuestCampus.Bff.csproj src/Bff/QuestCampus.Bff/
RUN dotnet restore src/Bff/QuestCampus.Bff/QuestCampus.Bff.csproj
COPY src/Bff/ src/Bff/
RUN dotnet publish src/Bff/QuestCampus.Bff/QuestCampus.Bff.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuestCampus.Bff.dll"]
