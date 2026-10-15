import type { NextFunction, Request, Response } from 'express';
import type { Config } from '../config.js';
import { sendProblem } from '../problem.js';
import { getAccessToken, verifyAccessToken, type AuthUser } from './tokens.js';

/** Le BFF est la porte d'entrée : aucune requête protégée ne passe sans JWT valide. */
export function requireAuth(config: Config) {
  return async (req: Request, res: Response, next: NextFunction): Promise<void> => {
    const token = getAccessToken(req);
    if (!token) return sendProblem(res, 401, 'Non authentifié', 'Veuillez vous connecter.');
    try {
      res.locals.user = await verifyAccessToken(token, config);
      res.locals.accessToken = token;
      next();
    } catch {
      sendProblem(res, 401, 'Non authentifié', 'Jeton invalide ou expiré.');
    }
  };
}

export function currentUser(res: Response): AuthUser {
  return res.locals.user as AuthUser;
}

export function currentToken(res: Response): string {
  return res.locals.accessToken as string;
}
