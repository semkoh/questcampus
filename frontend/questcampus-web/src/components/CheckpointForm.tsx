import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import { Alert } from './Alert';
import { getCurrentPosition } from './position';

const empty = { title: '', riddle: '', latitude: '', longitude: '', answer: '', points: '100' };

/** Formulaire organisateur : ajout d'un checkpoint (avec option "utiliser ma position"). */
export function CheckpointForm({ questId, onAdded }: { questId: string; onAdded: () => void }) {
  const [form, setForm] = useState(empty);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = (key: keyof typeof empty) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }));

  const useMyPosition = async () => {
    setError(null);
    try {
      const p = await getCurrentPosition();
      setForm((f) => ({ ...f, latitude: p.latitude.toFixed(6), longitude: p.longitude.toFixed(6) }));
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.addCheckpoint(questId, {
        title: form.title,
        riddle: form.riddle,
        answer: form.answer,
        latitude: Number(form.latitude),
        longitude: Number(form.longitude),
        points: Number(form.points),
      });
      setForm(empty);
      onAdded();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>Ajouter un checkpoint</h2>
      <Alert kind="error">{error}</Alert>
      <label>Titre<input value={form.title} onChange={set('title')} maxLength={120} required /></label>
      <label>Énigme<textarea value={form.riddle} onChange={set('riddle')} maxLength={1000} rows={3} required /></label>
      <label>Réponse attendue<input value={form.answer} onChange={set('answer')} maxLength={200} required /></label>
      <div className="grid-3">
        <label>Latitude<input type="number" step="any" min={-90} max={90} value={form.latitude} onChange={set('latitude')} required /></label>
        <label>Longitude<input type="number" step="any" min={-180} max={180} value={form.longitude} onChange={set('longitude')} required /></label>
        <label>Points<input type="number" min={1} max={1000} value={form.points} onChange={set('points')} required /></label>
      </div>
      <div className="row">
        <button type="button" className="btn btn-ghost" onClick={useMyPosition}>📍 Utiliser ma position</button>
        <button className="btn btn-primary" disabled={busy}>Ajouter</button>
      </div>
    </form>
  );
}
