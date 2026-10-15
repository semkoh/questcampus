import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { isOrganizer, type QuestDetail } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { Alert } from '../components/Alert';
import { AttemptForm } from '../components/AttemptForm';
import { CheckpointForm } from '../components/CheckpointForm';
import { Leaderboard } from '../components/Leaderboard';
import { TeamPanel } from '../components/TeamPanel';

export function QuestPage() {
  const { questId = '' } = useParams();
  const { user } = useAuth();
  const [quest, setQuest] = useState<QuestDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [openCheckpoint, setOpenCheckpoint] = useState<string | null>(null);

  const load = useCallback(() => {
    api.quest(questId).then(setQuest).catch((e: Error) => setError(e.message));
  }, [questId]);
  useEffect(load, [load]);

  const publish = async () => {
    setError(null);
    try {
      await api.publishQuest(questId);
      load();
    } catch (e) {
      setError((e as Error).message);
    }
  };

  if (!quest) return <div className="page-loading">{error ?? 'Chargement…'}</div>;

  const player = !isOrganizer(user);
  const validatedCount = quest.checkpoints.filter((c) => c.isValidatedByMyTeam).length;

  return (
    <div className="quest-page">
      <Link to="/" className="back">← Retour</Link>
      <header className="quest-header">
        <div>
          <h1>{quest.title} {!quest.isPublished && <span className="tag">Brouillon</span>}</h1>
          <p>{quest.description}</p>
        </div>
        {quest.isOwner && !quest.isPublished && (
          <button className="btn btn-primary" onClick={publish} disabled={quest.checkpoints.length === 0}>Publier la quête</button>
        )}
      </header>
      <Alert kind="error">{error}</Alert>

      <div className="columns">
        <section className="stack">
          {player && quest.myTeam && (
            <div className="progress">
              <span>{validatedCount} / {quest.checkpoints.length} checkpoints</span>
              <div className="progress-track">
                <div style={{ width: `${quest.checkpoints.length ? (validatedCount / quest.checkpoints.length) * 100 : 0}%` }} />
              </div>
            </div>
          )}

          {quest.checkpoints.length === 0 && <p className="muted">Aucun checkpoint pour l’instant.</p>}
          {quest.checkpoints.map((c) => (
            <article key={c.id} className={`card checkpoint ${c.isValidatedByMyTeam ? 'done' : ''}`}>
              <div className="checkpoint-head">
                <span className="cp-order">{c.order}</span>
                <h3>{c.title}</h3>
                <span className="cp-points">{c.points} pts</span>
              </div>
              <p className="riddle">{c.riddle}</p>

              {quest.isOwner && (
                <p className="owner-info">
                  Réponse : <strong>{c.answer}</strong> · {c.latitude?.toFixed(5)}, {c.longitude?.toFixed(5)}
                </p>
              )}

              {player && quest.myTeam && (c.isValidatedByMyTeam ? (
                <p className="validated">✔ Validé par votre équipe</p>
              ) : openCheckpoint === c.id ? (
                <AttemptForm questId={quest.id} checkpoint={c} onValidated={load} />
              ) : (
                <button className="btn btn-secondary" onClick={() => setOpenCheckpoint(c.id)}>Je suis sur place</button>
              ))}
            </article>
          ))}

          {quest.isOwner && !quest.isPublished && <CheckpointForm questId={quest.id} onAdded={load} />}
        </section>

        <aside className="stack">
          {player && <TeamPanel questId={quest.id} team={quest.myTeam} onChanged={load} />}
          {quest.isPublished && <Leaderboard questId={quest.id} myTeamId={quest.myTeam?.id} />}
        </aside>
      </div>
    </div>
  );
}
