import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { AttemptResult, Checkpoint } from '../api/types';
import { Alert } from './Alert';
import { DEMO_LOCATIONS, DEMO_MODE, getCurrentPosition, type Position } from './position';

/** Saisie de la réponse + récupération de la position, puis envoi au BFF. */
export function AttemptForm({ questId, checkpoint, onValidated }: {
  questId: string;
  checkpoint: Checkpoint;
  onValidated: () => void;
}) {
  const [answer, setAnswer] = useState('');
  const [demoIndex, setDemoIndex] = useState(-1); // -1 = GPS réel
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<AttemptResult | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      let position: Position;
      if (demoIndex >= 0) {
        position = DEMO_LOCATIONS[demoIndex].position;
      } else {
        setStatus('Localisation en cours…');
        position = await getCurrentPosition();
      }
      setStatus('Vérification…');
      const outcome = await api.submitAttempt(questId, checkpoint.id, answer, position.latitude, position.longitude);
      setResult(outcome);
      if (outcome.isValidated) onValidated();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setStatus(null);
      setBusy(false);
    }
  };

  return (
    <form className="attempt" onSubmit={submit}>
      <div className="attempt-row">
        <input value={answer} onChange={(e) => setAnswer(e.target.value)} placeholder="Votre réponse"
               maxLength={200} required aria-label={`Réponse au checkpoint ${checkpoint.order}`} />
        <button className="btn btn-primary" disabled={busy}>{busy ? '…' : 'Valider ici'}</button>
      </div>
      {DEMO_MODE && (
        <label className="demo-select">
          <span>Position (mode démo)</span>
          <select value={demoIndex} onChange={(e) => setDemoIndex(Number(e.target.value))}>
            <option value={-1}>GPS de l’appareil</option>
            {DEMO_LOCATIONS.map((l, i) => <option key={l.label} value={i}>{l.label}</option>)}
          </select>
        </label>
      )}
      {status && <p className="muted small">{status}</p>}
      <Alert kind="error">{error}</Alert>
      {result && (
        <div className={`result ${result.isValidated ? 'ok' : 'ko'}`} role="status">
          <strong>{result.message}</strong>
          {!result.alreadyValidated && (
            <ul>
              <li>{result.isAnswerCorrect ? '✔' : '✘'} Réponse</li>
              <li>{result.isWithinRadius ? '✔' : '✘'} Position ({result.distanceMeters.toFixed(0)} m du point, 30 m max)</li>
            </ul>
          )}
        </div>
      )}
    </form>
  );
}
