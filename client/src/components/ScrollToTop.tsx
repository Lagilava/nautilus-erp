import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

/** Resets scroll to the top of the page on every route change. Renders nothing. */
export function ScrollToTop() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, [pathname]);
  return null;
}
