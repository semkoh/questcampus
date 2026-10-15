import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { isOrganizer } from '../api/types';
import { useAuth } from '../auth/AuthContext';

export function Layout({ children }: { children: ReactNode }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate('/connexion');
  };

  return (
    <div className="shell">
      <header className="topbar">
        <Link to="/" className="brand">
          <span className="brand-pin" aria-hidden>◉</span> Quest Campus
        </Link>
        {user && (
          <div className="topbar-user">
            <span className={`role-badge ${isOrganizer(user) ? 'role-orga' : 'role-player'}`}>
              {isOrganizer(user) ? 'Organisateur' : 'Joueur'}
            </span>
            <span className="user-name">{user.displayName}</span>
            <button className="btn btn-ghost" onClick={handleLogout}>Déconnexion</button>
          </div>
        )}
      </header>
      <main className="content">{children}</main>
    </div>
  );
}
