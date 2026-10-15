import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// En développement, Vite relaie /bff, /api et /hubs vers le BFF (même origine => cookies HttpOnly OK).
const bff = process.env.BFF_URL ?? 'http://localhost:5000';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/bff': bff,
      '/api': bff,
      '/hubs': { target: bff, ws: true },
    },
  },
});
