import { Router, json } from 'express';
import { clearAuthCookies, writeAuthCookies, type AuthResponse } from '../auth/cookies.js';
import { currentUser, requireAuth } from '../auth/middleware.js';
import { REFRESH_COOKIE } from '../auth/tokens.js';
import type { DownstreamClient } from '../clients/downstream.js';
import type { Config } from '../config.js';
import { sendProblem } from '../problem.js';

/** Façade d'authentification : le BFF parle au service Auth et pose les cookies HttpOnly. */
export function authRoutes(config: Config, auth: DownstreamClient): Router {
  const router = Router();
  router.use(json({ limit: '10kb' }));

  router.post('/login', async (req, res) => {
    const response = await auth.post('/api/auth/login', undefined, req.body);
    if (!response.ok) return response.forward(res);
    res.json(writeAuthCookies(req, res, response.json<AuthResponse>()));
  });

  // Inscription puis connexion automatique (le formulaire contient déjà email + mot de passe).
  router.post('/register', async (req, res) => {
    const created = await auth.post('/api/auth/register', undefined, req.body);
    if (!created.ok) return created.forward(res);
    const login = await auth.post('/api/auth/login', undefined, { email: req.body.email, password: req.body.password });
    if (!login.ok) return login.forward(res);
    res.json(writeAuthCookies(req, res, login.json<AuthResponse>()));
  });

  router.post('/refresh', async (req, res) => {
    const refreshToken = req.cookies?.[REFRESH_COOKIE];
    if (!refreshToken) return sendProblem(res, 401, 'Session expirée', 'Veuillez vous reconnecter.');
    const response = await auth.post('/api/auth/refresh', undefined, { refreshToken });
    if (response.ok) return void res.json(writeAuthCookies(req, res, response.json<AuthResponse>()));
    clearAuthCookies(req, res);
    response.forward(res);
  });

  router.post('/logout', async (req, res) => {
    const refreshToken = req.cookies?.[REFRESH_COOKIE];
    if (refreshToken) await auth.post('/api/auth/logout', undefined, { refreshToken });
    clearAuthCookies(req, res);
    res.sendStatus(204);
  });

  // Profil lu directement dans le JWT validé : aucun appel réseau.
  router.get('/me', requireAuth(config), (_req, res) => {
    res.json(currentUser(res));
  });

  return router;
}
