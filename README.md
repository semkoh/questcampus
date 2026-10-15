# Quest Campus

Chasse au trésor géolocalisée. Les **organisateurs** créent des quêtes composées de checkpoints (énigme + position GPS + réponse + points). Les **joueurs** forment des équipes grâce à un code d'invitation, se rendent sur place et valident chaque checkpoint : il faut **la bonne réponse ET être à 30 m maximum du point**. Le classement se met à jour **en temps réel**.

Architecture demandée : **1 front, 3 microservices, 2 bases de données**.

```
Navigateur ──► web (React + nginx) ──► BFF (TypeScript) ──┬──► API Auth   (Python) ──► auth_db
                                                           └──► API Métier (Python) ──► quests_db
```

| Service            | Technologie                                   | Rôle |
|--------------------|-----------------------------------------------|------|
| `frontend`         | React 19, Vite, TypeScript, nginx             | Interface web (mobile d'abord) |
| `services/bff`     | TypeScript, Node.js 22, Express 5, Socket.IO  | Point d'entrée unique : sessions, proxy, agrégation, temps réel |
| `services/auth-api`| Python 3.12, FastAPI, SQLAlchemy, Argon2, JWT | Comptes, rôles, jetons |
| `services/quests-api` | Python 3.12, FastAPI, SQLAlchemy           | Quêtes, équipes, validations, classement (Clean Architecture) |
| `auth-db`, `quests-db` | PostgreSQL 17                             | Une base par API |

## Prérequis

| Pour…                          | Il faut                                        |
|--------------------------------|------------------------------------------------|
| Lancer l'application           | Docker Desktop (ou Docker Engine + Compose v2) |
| Développer / tester les API    | Python 3.12+                                   |
| Développer / tester BFF et front | Node.js 22                                   |

## Lancement (Docker)

```bash
cp .env.example .env          # puis remplacer JWT_SIGNING_KEY par un secret long
docker compose up --build
```

Ouvrir **http://localhost:8080**. Au premier démarrage, chaque API crée ses tables et ses données de démonstration.

Arrêt : `docker compose down` · Remise à zéro des données : `docker compose down -v`.

## Comptes de test

| Rôle          | Email                          | Mot de passe  | Remarque |
|---------------|--------------------------------|---------------|----------|
| Organisateur  | `organisateur@questcampus.fr`  | `Orga2026!`   | Propriétaire de la quête « Rallye du Marais » |
| Joueur        | `joueur1@questcampus.fr`       | `Joueur2026!` | Alice — sans équipe au départ |
| Joueur        | `joueur2@questcampus.fr`       | `Joueur2026!` | Bilal — sans équipe au départ |
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

**Démo en salle** : avec `DEMO_MODE=true` (par défaut), le formulaire de réponse propose un sélecteur « Position (mode démo) » avec ces lieux, plus un point à ~150 m pour montrer un refus. Autre solution : Chrome DevTools → *More tools* → *Sensors* → *Location*. Le serveur applique toujours la règle des 30 m.

## Tests

```bash
# API Métier : règle des 30 m, haversine, réponses, classement, cas d'usage (52 tests)
cd services/quests-api && pip install -r requirements-dev.txt && pytest

# API Auth : mots de passe, JWT, refresh tokens (12 tests)
cd services/auth-api && pip install -r requirements-dev.txt && pytest

# BFF : JWT, cookies, agrégation, mode dégradé, diffusion, rate limiting (9 tests)
cd services/bff && npm install && npm test
```

## Lancement sans Docker (développement)

Démarrer seulement les bases : `docker compose up auth-db quests-db`, puis dans quatre terminaux :

```bash
cd services/auth-api   && pip install -r requirements.txt && uvicorn app.main:app --port 8001
cd services/quests-api && pip install -r requirements.txt && uvicorn quests.api.main:app --port 8002
cd services/bff        && npm install && npm run dev                      # http://localhost:5000
cd frontend/questcampus-web && npm install && VITE_DEMO_MODE=true npm run dev   # http://localhost:5173
```

Les valeurs par défaut pointent déjà vers `localhost:5433` (auth_db) et `localhost:5434` (quests_db).

## Structure

```
docker-compose.yml, .env.example
frontend/questcampus-web/        React + Vite, servi par nginx (proxy /bff, /api, /hubs vers le BFF)
services/bff/src/
  auth/        validation JWT (jose), cookies HttpOnly, middleware
  clients/     appels HTTP vers les deux API
  routes/      /bff/auth/* (façade), /bff/dashboard (agrégation), tentatives (orchestration)
  proxy.ts     reverse proxy /api/quests/** et /api/teams/** vers l'API Métier
  realtime.ts  Socket.IO : classement en direct
services/auth-api/app/           FastAPI : models, security (Argon2, JWT), service, routes
services/quests-api/quests/
  domain/          entités, GeoPoint, haversine, règle de validation, classement — aucune dépendance
  application/     cas d'usage + ports (interfaces)
  infrastructure/  SQLAlchemy / PostgreSQL, générateur de codes, données de démo
  api/             routes FastAPI, vérification du JWT, erreurs ProblemDetails
docs/            schémas (architecture, bases, séquence) et captures d'écran
```

## Endpoints principaux

Le front ne parle qu'au **BFF**. Les jetons restent dans des cookies `HttpOnly; SameSite=Strict` : le JavaScript ne les voit jamais.

### BFF — endpoints propres

| Méthode | Route | Rôle |
|---------|-------|------|
| POST | `/bff/auth/login` | Connexion (appelle l'API Auth, pose les cookies) |
| POST | `/bff/auth/register` | Inscription Joueur + connexion automatique |
| POST | `/bff/auth/refresh` | Renouvellement de session (rotation du refresh token) |
| POST | `/bff/auth/logout` | Révocation + suppression des cookies |
| GET  | `/bff/auth/me` | Profil lu dans le JWT |
| GET  | `/bff/dashboard` | **Agrégation** : profil (Auth) + quêtes + équipes (Métier), mode dégradé si Auth est indisponible |
| POST | `/bff/quests/{questId}/checkpoints/{checkpointId}/attempts` | **Orchestration** : validation puis diffusion du classement (10 tentatives/min/joueur) |
| WS   | `/hubs/leaderboard` (Socket.IO) | `joinQuest(questId)` → événement `leaderboardUpdated` |

### API Métier (relayée par le BFF sous `/api/quests/**` et `/api/teams/**`)

| Méthode | Route | Accès |
|---------|-------|-------|
| GET  | `/api/quests` | Connecté |
| GET  | `/api/quests/{id}` | Connecté (réponses et coordonnées masquées aux joueurs) |
| POST | `/api/quests` | Organisateur |
| POST | `/api/quests/{id}/checkpoints` | Organisateur propriétaire |
| POST | `/api/quests/{id}/publish` | Organisateur propriétaire |
| POST | `/api/quests/{id}/teams` | Joueur |
| POST | `/api/teams/join` | Joueur |
| GET  | `/api/teams/mine` | Connecté |
| POST | `/api/quests/{id}/checkpoints/{cpId}/attempts` | Joueur (appelé par le BFF) |
| GET  | `/api/quests/{id}/leaderboard` | Connecté |

### API Auth (interne)

`POST /api/auth/register` · `POST /api/auth/login` · `POST /api/auth/refresh` · `POST /api/auth/logout` · `GET /api/auth/me`

Chaque service expose `GET /health`, et les deux API Python publient leur documentation interactive sur `/docs` (Swagger). Les erreurs suivent le format **ProblemDetails** (RFC 9457) : 400 règle métier, 401, 403, 404, 409 conflit, 423 compte verrouillé, 429 trop de tentatives, 503 service interne indisponible.

## Choix techniques (résumé)

- **BFF** : un seul point d'entrée adapté au front ; jetons hors de portée du JavaScript ; agrégation, orchestration et temps réel centralisés.
- **2 bases** : chaque API possède ses données (*database per service*) ; l'API Métier ne stocke que des `user_id` (claim `sub` du JWT).
- **Clean Architecture** dans l'API Métier : le domaine ne dépend ni de FastAPI ni de SQLAlchemy → règle métier testable seule.
- **JWT** HS256 (15 min) + refresh token rotatif (7 jours, haché en base, réutilisation = révocation de toutes les sessions) ; validé au BFF **et** revalidé par l'API Métier.
- **Socket.IO** : classement poussé aux joueurs qui regardent la quête.
- **Concurrence** : index unique partiel `attempts(team_id, checkpoint_id) WHERE is_success` → un checkpoint ne rapporte qu'une fois, même si deux coéquipiers valident au même instant (409 pour le second).

Les tables sont créées au démarrage (`create_all`) pour simplifier la démo ; en production on utiliserait des migrations Alembic.
