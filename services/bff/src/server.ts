import { createServer } from 'node:http';
import { createApp } from './app.js';
import { loadConfig } from './config.js';
import { attachRealtime } from './realtime.js';

const config = loadConfig();

// Le diffuseur Socket.IO est branché après la création du serveur HTTP.
let broadcast: (questId: string, leaderboard: unknown) => void = () => undefined;
const app = createApp({ config, broadcast: (questId, board) => broadcast(questId, board) });
const server = createServer(app);
broadcast = attachRealtime(server, config);

server.listen(config.port, () => {
  console.log(`[bff] à l'écoute sur le port ${config.port}`);
});
