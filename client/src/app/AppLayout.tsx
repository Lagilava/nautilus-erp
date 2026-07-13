import { useEffect, useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { LogOut, Menu, Search, X } from 'lucide-react';
import { Wordmark } from '../components/Brand';
import { useAuth } from '../auth/AuthContext';
import { NotificationBell } from '../notifications/NotificationBell';
import { ScrollToTop } from '../components/ScrollToTop';
import { CommandPalette } from '../components/CommandPalette';
import { AssistantWidget } from '../assistant/AssistantWidget';
import { NAV } from './nav';

export function AppLayout() {
  const { user, logout, hasRole } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);

  // Global Ctrl/Cmd+K opens the command palette from anywhere in the app.
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setPaletteOpen((o) => !o);
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  const initials = (user?.firstName?.[0] ?? '') + (user?.lastName?.[0] ?? '');

  const sidebar = (
    <nav className="flex h-full flex-col">
      <div className="px-5 py-5">
        <Wordmark />
      </div>
      <div className="flex-1 space-y-6 overflow-y-auto px-3 pb-6">
        {NAV.map((section) => {
          const items = section.items.filter((i) => !i.roles || i.roles.some((r) => hasRole(r)));
          if (items.length === 0) return null;
          return (
            <div key={section.heading}>
              <p className="px-3 pb-2 text-[10px] font-semibold uppercase tracking-[0.16em] text-ink-muted">
                {section.heading}
              </p>
              <ul className="space-y-0.5">
                {items.map((item) => (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end={item.to === '/'}
                      onClick={() => setMobileOpen(false)}
                      className={({ isActive }) =>
                        `flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                          isActive
                            ? 'bg-lagoon-50 text-lagoon-700'
                            : 'text-ink-soft hover:bg-lagoon-50/60 hover:text-ink'
                        }`
                      }
                    >
                      <item.icon className="h-[18px] w-[18px]" strokeWidth={2} />
                      {item.label}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </div>
    </nav>
  );

  return (
    <div className="flex min-h-screen bg-canvas">
      <ScrollToTop />
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 border-r border-line bg-surface lg:block">{sidebar}</aside>

      {/* Mobile drawer */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button
            type="button"
            aria-label="Close menu"
            className="absolute inset-0 bg-ink/30"
            onClick={() => setMobileOpen(false)}
          />
          <aside className="absolute left-0 top-0 h-full w-64 border-r border-line bg-surface">{sidebar}</aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-16 items-center justify-between border-b border-line bg-surface/80 px-4 backdrop-blur sm:px-6">
          <button className="btn-ghost -ml-2 p-2 lg:hidden" onClick={() => setMobileOpen((o) => !o)}>
            {mobileOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
          </button>
          <button
            type="button"
            onClick={() => setPaletteOpen(true)}
            className="hidden items-center gap-2 rounded-md border border-line px-3 py-1.5 text-sm text-ink-muted transition-colors hover:border-lagoon-300 hover:text-ink lg:flex"
          >
            <Search className="h-4 w-4" />
            Search…
            <kbd className="ml-4 rounded border border-line px-1.5 py-0.5 text-[10px] font-medium">Ctrl K</kbd>
          </button>
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => setPaletteOpen(true)}
              className="btn-ghost p-2 lg:hidden"
              aria-label="Search"
            >
              <Search className="h-[18px] w-[18px]" />
            </button>
            <NotificationBell />
            <div className="h-6 w-px bg-line" />
            <NavLink to="/profile" className="flex items-center gap-3 rounded-md px-2 py-1 hover:bg-lagoon-50/60" title="My profile">
              <div className="text-right">
                <div className="text-sm font-medium text-ink">
                  {user?.firstName} {user?.lastName}
                </div>
                <div className="text-xs text-ink-muted">{user?.roles.join(' · ')}</div>
              </div>
              <div className="flex h-9 w-9 items-center justify-center rounded-full bg-lagoon-500 text-sm font-semibold uppercase text-white">
                {initials || user?.email?.[0]?.toUpperCase()}
              </div>
            </NavLink>
            <button onClick={() => logout()} className="btn-ghost p-2" title="Sign out" aria-label="Sign out">
              <LogOut className="h-[18px] w-[18px]" />
            </button>
          </div>
        </header>

        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
          <Outlet />
        </main>
        <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} />
        <AssistantWidget />
      </div>
    </div>
  );
}
