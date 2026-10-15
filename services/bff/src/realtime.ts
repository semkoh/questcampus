import { parseCookie } from 'cookie';
import type { Server as HttpServer } from 'node:http';
import { Server } from 'socket.io';
import type { Config } from './config.js';
import { ACCESS_COOKIE, verifyAccessToken } from './auth/tokens.js';
import type { LeaderboardBroadcaster } from './routes/gameRoutes.js';

export const SOCKET_PATH = '/hubs/leaderboard';
const room = (questId: string) => `quest:${questId}`;

/**
 * Temps réel : chaque client rejoint la "room" de la quête qu'il regarde et reçoit
 * "leaderboardUpdated" dès qu'une équipe valide un checkpoint.
 */
export function attachRealtime(server: HttpServer, config: Config): LeaderboardBroadcaster {
  const io = new Server(server, { path: SOCKET_PATH, serveClient: false });

  // Même authentification que l'API : le cookie HttpOnly est envoyé avec la connexion WebSocket.
  io.use(async (socket, next) => {
    try {
      const cookies = parseCookie(socket.handshake.headers.cookie ?? '');
      const token = (socket.handshake.auth?.token as string | undefined) ?? cookies[ACCESS_COOKIE];
      if (!token) return next(new Error('Non authentifié'));
      socket.data.user = await verifyAccessToken(token, config);
      next();
    } catch {
      next(new Error('Non authentifié'));
    }
  });

  io.on('connection', (socket) => {
    socket.on('joinQuest', (questId: unknown) => {
      if (typeof questId === 'string' && questId.length <= 64) void socket.join(room(questId));
    });
    socket.on('leaveQuest', (questId: unknown) => {
      if (typeof questId === 'string') void socket.leave(room(questId));
    });
  });

  return (questId, leaderboard) => {
    io.to(room(questId)).emit('leaderboardUpdated', leaderboard);
  };
}
