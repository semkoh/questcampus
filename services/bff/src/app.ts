import cookieParser from 'cookie-parser';
import express, { type NextFunction, type Request, type Response } from 'express';
import { requireAuth } from './auth/middleware.js';
import { DownstreamClient, ServiceUnavailableError } from './clients/downstream.js';
import type { Config } from './config.js';
import { sendProblem } from './problem.js';
import { PROXIED_PATHS, questsProxy } from './proxy.js';
import { authRoutes } from './routes/authRoutes.js';
import { gameRoutes, type LeaderboardBroadcaster } from './routes/gameRoutes.js';

export interface AppDependencies {
  config: Config;
  auth?: DownstreamClient;
  quests?: DownstreamClient;
  broadcast?: LeaderboardBroadcaster;
}

export function createApp({ config, auth, quests, broadcast = () => undefined }: AppDependencies) {
  const authClient = auth ?? new DownstreamClient(config.authApiUrl);
  const questsClient = quests ?? new DownstreamClient(config.questsApiUrl);

  const app = express();
  app.disable('x-powered-by');
  app.set('trust proxy', 1); // derrière nginx : req.secure reflète X-Forwarded-Proto
  app.use(cookieParser());

  app.get('/health', (_req, res) => {
    res.json({ status: 'ok' });
  });
  app.use('/bff/auth', authRoutes(config, authClient));
  app.use('/bff', gameRoutes(config, authClient, questsClient, broadcast));

  // Reverse proxy : JWT validé ici, AVANT de relayer quoi que ce soit (pas de parsing du corps : il est relayé tel quel).
  app.use(PROXIED_PATHS, requireAuth(config));
  app.use(questsProxy(config.questsApiUrl));

  app.use((_req, res) => sendProblem(res, 404, 'Introuvable'));

  // Erreurs : service interne injoignable => 503 explicite ; le reste => 500 sans détail technique.
  app.use((error: unknown, req: Request, res: Response, _next: NextFunction) => {
    if (error instanceof ServiceUnavailableError) {
      console.warn(`[bff] ${req.method} ${req.path} : ${error.message}`);
      return sendProblem(res, 503, 'Service indisponible', 'Un service interne ne répond pas. Réessayez dans quelques instants.');
    }
    if (error instanceof SyntaxError) return sendProblem(res, 400, 'Requête invalide', 'Corps JSON invalide.');
    console.error(`[bff] ${req.method} ${req.path}`, error);
    sendProblem(res, 500, 'Erreur interne', 'Une erreur inattendue est survenue.');
  });

  return app;
}
