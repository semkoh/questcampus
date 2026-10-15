# Quest Campus

Chasse au trésor géolocalisée. Les **organisateurs** créent des quêtes composées de checkpoints (énigme + position GPS + réponse + points). Les **joueurs** forment des équipes grâce à un code d'invitation, se rendent sur place et valident chaque checkpoint : il faut **la bonne réponse ET être à 30 m maximum du point**. Le classement se met à jour **en temps réel**.

Projet du module *Architecture .NET* — 3 microservices .NET 10, 2 bases PostgreSQL, 1 front React.

```
Navigateur ──► web (nginx + React) ──► BFF (YARP + SignalR + agrégation) ──┬──► Auth API  ──► auth_db
                                                                           └──► Quests API ──► quests_db
```

## Prérequis

| Pour…                         | Il faut                                         |
|-------------------------------|-------------------------------------------------|
| Lancer l'application          | Docker Desktop (ou Docker Engine + Compose v2)  |
| Compiler / tester hors Docker | SDK .NET 10 (`dotnet --version` ≥ 10.0.100)     |
| Développer le front           | Node.js 22                                      |

## Lancement (Docker)

```bash
cp .env.example .env          # puis remplacer JWT_SIGNING_KEY par un secret long
docker compose up --build
```

Ouvrir **http://localhost:8080**. Au premier démarrage, chaque service crée son schéma et ses données de démonstration (comptez ~1 min pour le build initial).

Arrêt : `docker compose down` · Remise à zéro des données : `docker compose down -v`.

## Comptes de test

| Rôle          | Email                          | Mot de passe  | Remarque                                  |
|---------------|--------------------------------|---------------|-------------------------------------------|
| Organisateur  | `organisateur@questcampus.fr`  | `Orga2026!`   | Propriétaire de la quête « Rallye du Marais » |
| Joueur        | `joueur1@questcampus.fr`       | `Joueur2026!` | Alice — sans équipe au départ             |
| Joueur        | `joueur2@questcampus.fr`       | `Joueur2026!` | Bilal — sans équipe au départ             |
| Joueur        | `joueur3@questcampus.fr`       | `Joueur2026!` | Chloé — équipe « Les Éclaireurs » (code `ECLR42`, 100 pts) |

L'écran de connexion propose des boutons pour pré-remplir ces comptes. L'inscription crée toujours un compte **Joueur**.

### Réponses de la quête de démonstration

| # | Checkpoint                 | Réponse         | Position (lat, lon)   | Points |
|---|----------------------------|-----------------|-----------------------|--------|
| 1 | Le parvis                  | Hôtel de Ville  | 48.85646, 2.35242     | 100    |
| 2 | La tour solitaire          | Saint-Jacques   | 48.85797, 2.34888     | 150    |
| 3 | Les sculptures qui dansent | Stravinsky      | 48.85928, 2.35139     | 200    |
| 4 | Le musée à l'envers        | Pompidou        | 48.86061, 2.35225     | 250    |

La comparaison ignore casse, accents, espaces et ponctuation (`hotel de ville` est accepté).

**Démo en salle** : avec `DEMO_MODE=true` (par défaut), le formulaire de réponse affiche un sélecteur « Position (mode démo) » listant ces lieux, plus un point situé à ~150 m pour montrer un refus. Alternative : Chrome DevTools → *More tools* → *Sensors* → *Location* pour simuler un GPS. Mettre `DEMO_MODE=false` pour n'utiliser que le GPS réel.

## Compiler et tester sans Docker

```bash
dotnet build QuestCampus.slnx
dotnet test  QuestCampus.slnx
```

Lancement local des services (deux PostgreSQL sur les ports 5433/5434, par exemple via `docker compose up auth-db quests-db`) :

```bash
dotnet run --project src/Auth/QuestCampus.Auth.Api        # http://localhost:5001
dotnet run --project src/Quests/QuestCampus.Quests.Api    # http://localhost:5002
dotnet run --project src/Bff/QuestCampus.Bff              # http://localhost:5000
cd src/Front/questcampus-web && npm install && VITE_DEMO_MODE=true npm run dev   # http://localhost:5173
```

## Structure

```
QuestCampus.slnx
Directory.Build.props / Directory.Packages.props   paramètres et versions NuGet centralisés
docker-compose.yml, .env.example
src/
  Auth/QuestCampus.Auth.Api             Identity + JWT + refresh tokens        → auth_db
  Quests/QuestCampus.Quests.Domain        entités, règle de validation, classement (aucune dépendance)
  Quests/QuestCampus.Quests.Application   cas d'usage + interfaces (ports)
  Quests/QuestCampus.Quests.Infrastructure EF Core / PostgreSQL (adaptateurs)  → quests_db
  Quests/QuestCampus.Quests.Api           minimal APIs, JWT, ProblemDetails
  Bff/QuestCampus.Bff                     YARP, SignalR, agrégation, cookies HttpOnly, rate limiting
  Front/questcampus-web                   React + Vite + TypeScript, servi par nginx
tests/QuestCampus.Quests.Tests            xUnit : haversine, règle 30 m + réponse, classement, cas d'usage
docs/                                     schémas d'architecture et des bases
```

## Endpoints principaux

Le front ne parle qu'au **BFF** (même origine via nginx). Les jetons sont stockés dans des cookies `HttpOnly; SameSite=Strict`.

### BFF — endpoints propres

| Méthode | Route                                                        | Rôle                                                        |
|---------|--------------------------------------------------------------|-------------------------------------------------------------|
| POST    | `/bff/auth/login`                                            | Connexion (appelle Auth API, pose les cookies)              |
| POST    | `/bff/auth/register`                                         | Inscription Joueur + connexion automatique                  |
| POST    | `/bff/auth/refresh`                                          | Rotation du refresh token                                   |
| POST    | `/bff/auth/logout`                                           | Révocation + suppression des cookies                        |
| GET     | `/bff/auth/me`                                               | Profil lu dans le JWT                                       |
| GET     | `/bff/dashboard`                                             | **Agrégation** : profil (Auth) + quêtes + équipes (Métier)  |
| POST    | `/bff/quests/{questId}/checkpoints/{checkpointId}/attempts`  | **Orchestration** : validation puis diffusion SignalR (10/min/joueur) |
| WS      | `/hubs/leaderboard`                                          | Hub SignalR : `JoinQuest(questId)` → `LeaderboardUpdated`   |

### API Métier (relayée par YARP sous `/api/quests/**` et `/api/teams/**`)

| Méthode | Route                                                  | Accès         |
|---------|--------------------------------------------------------|---------------|
| GET     | `/api/quests`                                          | Authentifié   |
| GET     | `/api/quests/{id}`                                     | Authentifié (réponses/coordonnées masquées aux joueurs) |
| POST    | `/api/quests`                                          | Organisateur  |
| POST    | `/api/quests/{id}/checkpoints`                         | Organisateur propriétaire |
| POST    | `/api/quests/{id}/publish`                             | Organisateur propriétaire |
| POST    | `/api/quests/{id}/teams`                               | Joueur        |
| POST    | `/api/teams/join`                                      | Joueur        |
| GET     | `/api/teams/mine`                                      | Authentifié   |
| POST    | `/api/quests/{id}/checkpoints/{cpId}/attempts`         | Joueur (appelé par le BFF) |
| GET     | `/api/quests/{id}/leaderboard`                         | Authentifié   |

### API Auth (interne)

`POST /api/auth/register` · `POST /api/auth/login` · `POST /api/auth/refresh` · `POST /api/auth/logout` · `GET /api/auth/me`

Chaque service expose aussi `GET /health`. Les erreurs sont renvoyées au format **ProblemDetails** (RFC 9457) : 400 règle métier, 401, 403, 404, 409 conflit, 429 trop de tentatives, 503 service interne indisponible.

## Choix techniques (résumé)

- **BFF** : un seul point d'entrée adapté au front ; jetons hors de portée du JavaScript ; agrégation et temps réel centralisés.
- **2 bases** : chaque microservice possède ses données (*database per service*) ; le Métier ne stocke que des `userId`.
- **Clean Architecture** : la règle métier (domaine) ne dépend ni d'EF Core ni d'ASP.NET → testable en isolation.
- **JWT** HS256 (15 min) + refresh token rotatif (7 jours, haché en base, détection de réutilisation) ; validé au BFF **et** revalidé par l'API Métier.
- **SignalR** : classement poussé aux clients abonnés au groupe de la quête.
- **Concurrence** : index unique partiel `(TeamId, CheckpointId) WHERE IsSuccess` → un checkpoint ne rapporte qu'une fois, même en cas de double clic simultané.

Le schéma est créé au démarrage par `EnsureCreated` (simplicité de la démo) ; voir le dossier d'architecture pour les améliorations prévues (migrations EF Core, RS256 + JWKS, cache Redis, etc.).
