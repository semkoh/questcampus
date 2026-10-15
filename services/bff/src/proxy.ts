import type { Request, Response } from 'express';
import { createProxyMiddleware } from 'http-proxy-middleware';
import type { ClientRequest } from 'node:http';
import { sendProblem } from './problem.js';

export const PROXIED_PATHS = ['/api/quests', '/api/teams'];

/**
 * Reverse proxy vers l'API Métier. Le JWT (validé juste avant) est injecté en en-tête Authorization
 * et les cookies sont retirés : ils ne quittent jamais le BFF.
 */
export function questsProxy(target: string) {
  return createProxyMiddleware<Request, Response>({
    target,
    changeOrigin: true,
    pathFilter: PROXIED_PATHS,
    on: {
      proxyReq: (proxyReq: ClientRequest, _req: Request, res: Response) => {
        proxyReq.removeHeader('cookie');
        proxyReq.setHeader('authorization', `Bearer ${res.locals.accessToken as string}`);
      },
      error: (_err, _req, res) => {
        if ('status' in res && !res.headersSent) {
          sendProblem(res as Response, 503, 'Service indisponible', "L'API Métier ne répond pas. Réessayez dans quelques instants.");
        }
      },
    },
  });
}
