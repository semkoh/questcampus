import { Router, json } from 'express';
import { rateLimit } from 'express-rate-limit';
import { currentToken, currentUser, requireAuth } from '../auth/middleware.js';
import { ServiceUnavailableError, type DownstreamClient } from '../clients/downstream.js';
import type { Config } from '../config.js';
import { sendProblem } from '../problem.js';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Diffusion temps réel du classement (implémentée avec Socket.IO, remplaçable en test). */
export type LeaderboardBroadcaster = (questId: string, leaderboard: unknown) => void;

/** Endpoints à valeur ajoutée du BFF, au-delà du simple reverse proxy. */
export function gameRoutes(config: Config, auth: DownstreamClient, quests: DownstreamClient,
                           broadcast: LeaderboardBroadcaster): Router {
  const router = Router();
  router.use(json({ limit: '10kb' }), requireAuth(config));

  // AGRÉGATION : un seul appel du front => 3 appels parallèles vers 2 services différents.
  router.get('/dashboard', async (_req, res) => {
    const token = currentToken(res);
    const [profile, questList, teams] = await Promise.allSettled([
      auth.get('/api/auth/me', token),
      quests.get('/api/quests', token),
      quests.get('/api/teams/mine', token),
    ]);

    if (questList.status === 'rejected') throw questList.reason;
    if (teams.status === 'rejected') throw teams.reason;
    if (!questList.value.ok) return questList.value.forward(res);
    if (!teams.value.ok) return teams.value.forward(res);

    // Mode dégradé : si le service Auth ne répond pas, on se contente des informations du JWT.
    const user = profile.status === 'fulfilled' && profile.value.ok ? profile.value.json() : currentUser(res);
    const questItems = questList.value.json<unknown[]>();
    const teamItems = teams.value.json<unknown[]>();
    res.json({
      user,
      quests: questItems,
      myTeams: teamItems,
      stats: { availableQuests: questItems.length, myTeamCount: teamItems.length },
    });
  });

  // Anti-force brute sur les réponses : N tentatives par minute et par joueur.
  const attemptsLimiter = rateLimit({
    windowMs: 60_000,
    limit: config.attemptsPerMinute,
    standardHeaders: 'draft-8',
    legacyHeaders: false,
    keyGenerator: (_req, res) => currentUser(res).id,
    handler: (_req, res) => sendProblem(res, 429, 'Trop de tentatives', 'Patientez une minute avant de réessayer.'),
  });

  // ORCHESTRATION : validation par l'API Métier puis diffusion du classement en temps réel.
  router.post('/quests/:questId/checkpoints/:checkpointId/attempts', attemptsLimiter, async (req, res) => {
    const questId = String(req.params.questId);
    const checkpointId = String(req.params.checkpointId);
    if (!UUID.test(questId) || !UUID.test(checkpointId)) return sendProblem(res, 404, 'Introuvable');
    const token = currentToken(res);
    const result = await quests.post(`/api/quests/${questId}/checkpoints/${checkpointId}/attempts`, token, req.body);

    if (result.ok && result.json<{ isValidated: boolean }>().isValidated) {
      try {
        const leaderboard = await quests.get(`/api/quests/${questId}/leaderboard`, token);
        if (leaderboard.ok) broadcast(questId, leaderboard.json());
      } catch (error) {
        // La validation est enregistrée : un échec de diffusion ne doit pas la faire échouer.
        if (!(error instanceof ServiceUnavailableError)) throw error;
      }
    }
    result.forward(res);
  });

  return router;
}
