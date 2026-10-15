import { useEffect, useRef, useState } from 'react';
import { io } from 'socket.io-client';
import { api } from '../api/client';
import type { Leaderboard as LeaderboardData } from '../api/types';

/**
 * Classement en temps réel : chargement initial via REST, puis mises à jour poussées
 * par le BFF via Socket.IO ("leaderboardUpdated") à chaque checkpoint validé.
 */
export function Leaderboard({ questId, myTeamId }: { questId: string; myTeamId?: string }) {
  const [board, setBoard] = useState<LeaderboardData | null>(null);
  const [live, setLive] = useState(false);
  const [flashTeam, setFlashTeam] = useState<string | null>(null);
  const previous = useRef<Map<string, number>>(new Map());

  useEffect(() => {
    let disposed = false;
    api.leaderboard(questId).then((b) => !disposed && setBoard(b)).catch(() => undefined);

    // Même origine : le cookie HttpOnly de session accompagne la connexion WebSocket.
    const socket = io({ path: '/hubs/leaderboard', withCredentials: true });
    socket.on('connect', () => {
      socket.emit('joinQuest', questId); // aussi rejoué après chaque reconnexion automatique
      if (!disposed) setLive(true);
    });
    socket.on('disconnect', () => setLive(false));
    socket.on('connect_error', () => setLive(false));
    socket.on('leaderboardUpdated', (update: LeaderboardData) => {
      if (update.questId === questId) setBoard(update);
    });

    return () => {
      disposed = true;
      socket.emit('leaveQuest', questId);
      socket.disconnect();
    };
  }, [questId]);

  // Met en surbrillance l'équipe dont le score vient de changer.
  useEffect(() => {
    if (!board) return;
    const changed = board.entries.find((e) => (previous.current.get(e.teamId) ?? e.points) !== e.points);
    previous.current = new Map(board.entries.map((e) => [e.teamId, e.points]));
    if (changed) {
      setFlashTeam(changed.teamId);
      const timer = setTimeout(() => setFlashTeam(null), 2500);
      return () => clearTimeout(timer);
    }
  }, [board]);

  return (
    <section className="card leaderboard">
      <div className="card-head">
        <h2>Classement</h2>
        <span className={`live-dot ${live ? 'on' : ''}`}>{live ? 'En direct' : 'Hors ligne'}</span>
      </div>
      {!board || board.entries.length === 0 ? (
        <p className="muted">Aucune équipe inscrite pour le moment.</p>
      ) : (
        <ol className="ranking">
          {board.entries.map((entry) => (
            <li
              key={entry.teamId}
              className={[
                'ranking-row',
                entry.teamId === myTeamId ? 'mine' : '',
                entry.teamId === flashTeam ? 'flash' : '',
              ].join(' ')}
            >
              <span className={`rank rank-${entry.rank}`}>{entry.rank}</span>
              <span className="team">
                <strong>{entry.teamName}</strong>
                <small>{entry.memberCount} joueur{entry.memberCount > 1 ? 's' : ''} · {entry.validatedCheckpoints} checkpoint{entry.validatedCheckpoints > 1 ? 's' : ''}</small>
              </span>
              <span className="score">
                {entry.points}<small> / {board.maxPoints}</small>
              </span>
              <span className="bar" style={{ width: `${board.maxPoints ? (entry.points / board.maxPoints) * 100 : 0}%` }} />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
