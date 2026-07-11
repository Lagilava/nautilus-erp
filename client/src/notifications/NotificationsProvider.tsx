import { createContext, useContext, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import { tokenStore } from '../lib/tokenStore';
import { useAuth } from '../auth/AuthContext';

export interface AppNotification {
  id: number;
  title: string;
  message: string;
  level: string;
  at: Date;
}

interface NotificationsState {
  items: AppNotification[];
  unread: number;
  markAllRead: () => void;
}

const NotificationsContext = createContext<NotificationsState>({
  items: [],
  unread: 0,
  markAllRead: () => {},
});

export function NotificationsProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [items, setItems] = useState<AppNotification[]>([]);
  const [unread, setUnread] = useState(0);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    if (!isAuthenticated) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/notifications', { accessTokenFactory: () => tokenStore.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on(
      'notification',
      (payload: { title: string; message: string; level: string; entityType?: string; entityId?: string }) => {
        setItems((prev) => [
          { id: Date.now() + Math.random(), at: new Date(), ...payload },
          ...prev,
        ].slice(0, 30));
        setUnread((u) => u + 1);

        // Let any page showing this entity refetch it instead of going stale until reload —
        // mirrors the erp:unauthorized pattern in AuthContext rather than adding a new store.
        if (payload.entityType) {
          window.dispatchEvent(
            new CustomEvent('erp:entity-updated', {
              detail: { entityType: payload.entityType, entityId: payload.entityId },
            }),
          );
        }
      },
    );

    connection.start().catch(() => {
      /* hub unavailable — notifications are non-critical */
    });
    connectionRef.current = connection;

    return () => {
      if (connection.state !== HubConnectionState.Disconnected) connection.stop();
      connectionRef.current = null;
    };
  }, [isAuthenticated]);

  return (
    <NotificationsContext.Provider value={{ items, unread, markAllRead: () => setUnread(0) }}>
      {children}
    </NotificationsContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useNotifications() {
  return useContext(NotificationsContext);
}
