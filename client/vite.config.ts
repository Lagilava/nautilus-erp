import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// `dotnet run` uses the default http profile (http://localhost:5126). Proxy /api and /hubs
// there so the browser talks same-origin over plain HTTP — no CORS or dev-cert friction.
// Override with VITE_API_TARGET (e.g. the https profile) if needed.
const API_TARGET = process.env.VITE_API_TARGET ?? 'http://localhost:5126';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: API_TARGET, changeOrigin: true, secure: false },
      '/hubs': { target: API_TARGET, changeOrigin: true, secure: false, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    globals: true,
    css: false,
  },
});
