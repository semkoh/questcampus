import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { MyTeam } from '../api/types';
import { Alert } from './Alert';

/** Créer une équipe pour la quête ou en rejoindre une avec un code d'invitation. */
export function TeamPanel({ questId, team, onChanged }: { questId: string; team: MyTeam | null; onChanged: () => void }) {
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (team) {
    return (
      <section className="card team-card">
        <h2>Mon équipe</h2>
        <p className="team-name">{team.name}</p>
        <p className="muted">{team.memberCount} membre{team.memberCount > 1 ? 's' : ''}</p>
        <div className="invite">
          <span>Code d’invitation</span>
          <code>{team.inviteCode}</code>
        </div>
        <p className="muted small">Partagez ce code : vos coéquipiers le saisissent pour vous rejoindre.</p>
      </section>
    );
  }

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const create = (e: FormEvent) => { e.preventDefault(); run(() => api.createTeam(questId, name)); };
  const join = (e: FormEvent) => { e.preventDefault(); run(() => api.joinTeam(code)); };

  return (
    <section className="card team-card">
      <h2>Rejoindre la partie</h2>
      <Alert kind="error">{error}</Alert>
      <form onSubmit={create} className="stack">
        <label>Créer une équipe
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Nom de l’équipe" maxLength={60} required />
        </label>
        <button className="btn btn-primary" disabled={busy}>Créer</button>
      </form>
      <div className="divider"><span>ou</span></div>
      <form onSubmit={join} className="stack">
        <label>Code d’invitation
          <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase())} placeholder="EX : K7MQ2P"
                 maxLength={6} className="code-input" required />
        </label>
        <button className="btn btn-secondary" disabled={busy}>Rejoindre</button>
      </form>
    </section>
  );
}
