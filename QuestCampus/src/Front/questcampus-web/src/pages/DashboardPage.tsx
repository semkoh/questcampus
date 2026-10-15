import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api/client';
import { isOrganizer, type Dashboard } from '../api/types';
import { Alert } from '../components/Alert';

export function DashboardPage() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const navigate = useNavigate();

  // Un seul appel : le BFF agrège profil (Auth) + quêtes + équipes (Métier).
  const load = useCallback(() => {
    api.dashboard().then(setData).catch((e: Error) => setError(e.message));
  }, []);
  useEffect(load, [load]);

  const join = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const team = await api.joinTeam(code);
      navigate(`/quetes/${team.questId}`);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const createQuest = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const quest = await api.createQuest(title, description);
      navigate(`/quetes/${quest.id}`);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  if (!data) return <div className="page-loading">{error ?? 'Chargement…'}</div>;
  const organizer = isOrganizer(data.user);

  return (
    <div className="dashboard">
      <section className="welcome">
        <h1>Bonjour {data.user.displayName} 👋</h1>
        <div className="stats">
          <div><strong>{data.stats.availableQuests}</strong><span>{organizer ? 'quêtes créées' : 'quêtes ouvertes'}</span></div>
          {!organizer && <div><strong>{data.stats.myTeamCount}</strong><span>équipes</span></div>}
        </div>
      </section>

      <Alert kind="error">{error}</Alert>

      <div className="columns">
        <section className="stack">
          <h2>{organizer ? 'Mes quêtes' : 'Quêtes disponibles'}</h2>
          {data.quests.length === 0 && <p className="muted">Aucune quête pour le moment.</p>}
          {data.quests.map((q) => (
            <Link key={q.id} to={`/quetes/${q.id}`} className="card quest-card">
              <div className="quest-card-head">
                <h3>{q.title}</h3>
                {!q.isPublished && <span className="tag">Brouillon</span>}
              </div>
              <p>{q.description}</p>
              <div className="quest-meta">
                <span>{q.checkpointCount} checkpoint{q.checkpointCount > 1 ? 's' : ''}</span>
                <span>{q.totalPoints} pts</span>
              </div>
            </Link>
          ))}
        </section>

        <aside className="stack">
          {organizer ? (
            <form className="card stack" onSubmit={createQuest}>
              <h2>Nouvelle quête</h2>
              <label>Titre<input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={120} required /></label>
              <label>Description<textarea value={description} onChange={(e) => setDescription(e.target.value)} maxLength={2000} rows={3} /></label>
              <button className="btn btn-primary">Créer la quête</button>
            </form>
          ) : (
            <>
              <form className="card stack" onSubmit={join}>
                <h2>Rejoindre une équipe</h2>
                <label>Code d’invitation
                  <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} maxLength={6}
                         className="code-input" placeholder="EX : K7MQ2P" required />
                </label>
                <button className="btn btn-secondary">Rejoindre</button>
              </form>
              <section className="card">
                <h2>Mes équipes</h2>
                {data.myTeams.length === 0 && <p className="muted">Vous n’avez pas encore d’équipe.</p>}
                <ul className="team-list">
                  {data.myTeams.map((t) => (
                    <li key={t.id}>
                      <Link to={`/quetes/${t.questId}`}><strong>{t.name}</strong> — {t.questTitle}</Link>
                      <code>{t.inviteCode}</code>
                    </li>
                  ))}
                </ul>
              </section>
            </>
          )}
        </aside>
      </div>
    </div>
  );
}
