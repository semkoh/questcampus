import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { Alert } from '../components/Alert';

// Comptes créés automatiquement par le service Auth (voir README).
const DEMO_ACCOUNTS = [
  { label: 'Organisatrice', email: 'organisateur@questcampus.fr', password: 'Orga2026!' },
  { label: 'Alice (joueuse)', email: 'joueur1@questcampus.fr', password: 'Joueur2026!' },
  { label: 'Bilal (joueur)', email: 'joueur2@questcampus.fr', password: 'Joueur2026!' },
];

export function LoginPage() {
  const { login, register } = useAuth();
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === 'login') await login(email, password);
      else await register(email, password, displayName);
      navigate('/');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-screen">
      <div className="login-hero">
        <h1>Quest Campus</h1>
        <p>La chasse au trésor géolocalisée : résolvez l’énigme, rendez-vous sur place, faites grimper votre équipe au classement.</p>
      </div>
      <form className="card login-card stack" onSubmit={submit}>
        <div className="tabs" role="tablist">
          <button type="button" role="tab" aria-selected={mode === 'login'} className={mode === 'login' ? 'active' : ''}
                  onClick={() => setMode('login')}>Connexion</button>
          <button type="button" role="tab" aria-selected={mode === 'register'} className={mode === 'register' ? 'active' : ''}
                  onClick={() => setMode('register')}>Inscription</button>
        </div>
        <Alert kind="error">{error}</Alert>
        {mode === 'register' && (
          <label>Pseudo<input value={displayName} onChange={(e) => setDisplayName(e.target.value)} maxLength={60} required /></label>
        )}
        <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" required /></label>
        <label>Mot de passe
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)}
                 autoComplete={mode === 'login' ? 'current-password' : 'new-password'} required />
        </label>
        {mode === 'register' && <p className="muted small">8 caractères minimum, avec majuscule, minuscule, chiffre et symbole.</p>}
        <button className="btn btn-primary" disabled={busy}>{mode === 'login' ? 'Se connecter' : 'Créer mon compte'}</button>

        {mode === 'login' && (
          <div className="demo-accounts">
            <span className="muted small">Comptes de démonstration</span>
            <div className="row wrap">
              {DEMO_ACCOUNTS.map((a) => (
                <button key={a.email} type="button" className="chip"
                        onClick={() => { setEmail(a.email); setPassword(a.password); }}>{a.label}</button>
              ))}
            </div>
          </div>
        )}
      </form>
    </div>
  );
}
