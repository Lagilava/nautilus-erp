import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The API runs on https://localhost:7203 in Development (see launchSettings.json).
// Proxy /api and /hubs there so the browser talks same-origin (no CORS/cert friction in dev).
const API_TARGET = process.env.VITE_API_TARGET ?? 'https://localhost:7203';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: API_TARGET, changeOrigin: true, secure: false },
      '/hubs': { target: API_TARGET, changeOrigin: true, secure: false, ws: true },
    },
  },
});
