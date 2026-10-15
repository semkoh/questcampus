import type { Request, Response, CookieOptions } from 'express';
import { ACCESS_COOKIE, REFRESH_COOKIE } from './tokens.js';

/** Réponse du service Auth à /login et /refresh. */
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: unknown;
}

const REFRESH_PATH = '/bff/auth';

function options(req: Request, path: string, expires?: Date): CookieOptions {
  return {
    httpOnly: true,        // invisible du JavaScript : protège contre le vol de jeton par XSS
    sameSite: 'strict',    // jamais envoyé depuis un autre site : protège contre le CSRF
    secure: req.secure,    // HTTPS uniquement quand le site est servi en HTTPS
    path,
    expires,
  };
}

/**
 * Les jetons ne sont jamais renvoyés au navigateur dans le corps de la réponse :
 * ils sont placés dans deux cookies HttpOnly gérés par le BFF.
 */
export function writeAuthCookies(req: Request, res: Response, auth: AuthResponse): unknown {
  const expires = new Date(auth.refreshTokenExpiresAtUtc);
  // Le cookie vit aussi longtemps que la session ; le JWT porte lui-même sa courte expiration (15 min).
  res.cookie(ACCESS_COOKIE, auth.accessToken, options(req, '/', expires));
  res.cookie(REFRESH_COOKIE, auth.refreshToken, options(req, REFRESH_PATH, expires));
  return auth.user;
}

export function clearAuthCookies(req: Request, res: Response): void {
  res.clearCookie(ACCESS_COOKIE, options(req, '/'));
  res.clearCookie(REFRESH_COOKIE, options(req, REFRESH_PATH));
}
