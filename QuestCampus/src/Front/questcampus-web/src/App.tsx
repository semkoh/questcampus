import type { ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import { Layout } from './components/Layout';
import { DashboardPage } from './pages/DashboardPage';
import { LoginPage } from './pages/LoginPage';
import { QuestPage } from './pages/QuestPage';

function RequireAuth({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) return <div className="page-loading">Chargement…</div>;
  return user ? <Layout>{children}</Layout> : <Navigate to="/connexion" replace />;
}

export function App() {
  const { user, loading } = useAuth();
  return (
    <Routes>
      <Route path="/connexion" element={!loading && user ? <Navigate to="/" replace /> : <LoginPage />} />
      <Route path="/" element={<RequireAuth><DashboardPage /></RequireAuth>} />
      <Route path="/quetes/:questId" element={<RequireAuth><QuestPage /></RequireAuth>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
