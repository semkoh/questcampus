import type { Request } from 'express';
import { jwtVerify } from 'jose';
import type { Config } from '../config.js';

export const ACCESS_COOKIE = 'qc_access';
export const REFRESH_COOKIE = 'qc_refresh';

/** Utilisateur connu du BFF, lu dans le JWT validé. */
export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

/** Jeton d'accès : en-tête Authorization (outils, tests) sinon cookie HttpOnly (navigateur). */
export function getAccessToken(req: Pick<Request, 'headers' | 'cookies'>): string | undefined {
  const header = req.headers.authorization;
  if (header?.toLowerCase().startsWith('bearer ')) return header.slice(7).trim();
  return req.cookies?.[ACCESS_COOKIE];
}

/**
 * Vérifie signature HS256, émetteur, audience et expiration (30 s de tolérance d'horloge).
 * Lève une erreur si le jeton est invalide.
 */
export async function verifyAccessToken(token: string, config: Config): Promise<AuthUser> {
  const { payload } = await jwtVerify(token, new TextEncoder().encode(config.jwtSigningKey), {
    algorithms: ['HS256'],
    issuer: config.jwtIssuer,
    audience: config.jwtAudience,
    clockTolerance: 30,
    requiredClaims: ['sub', 'exp'],
  });
  const role = payload.role;
  return {
    id: String(payload.sub),
    email: String(payload.email ?? ''),
    displayName: String(payload.name ?? ''),
    roles: Array.isArray(role) ? role.map(String) : role ? [String(role)] : [],
  };
}
