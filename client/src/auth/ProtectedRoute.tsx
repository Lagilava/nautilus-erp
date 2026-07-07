import { Navigate, useLocation } from 'react-router-dom';
import type { ReactNode } from 'react';
import { useAuth } from './AuthContext';
import { Loading } from '../components/ui';

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, ready } = useAuth();
  const location = useLocation();

  if (!ready) return <Loading label="Starting Nautilus…" />;
  if (!isAuthenticated) return <Navigate to="/login" state={{ from: location }} replace />;
  return <>{children}</>;
}
