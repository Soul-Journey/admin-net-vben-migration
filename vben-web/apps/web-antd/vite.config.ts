import { defineConfig } from '@vben/vite-config';
import { loadEnv } from 'vite';

export default defineConfig(async ({ mode }) => {
  const env = loadEnv(mode, process.cwd());
  const proxyTarget = env.VITE_PROXY_TARGET || 'http://localhost:5005/api';
  const uploadTarget = proxyTarget.replace(/\/api\/?$/, '');

  return {
    application: {},
    vite: {
      server: {
        proxy: {
          '/api': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            target: proxyTarget,
            ws: true,
          },
          '/upload': {
            changeOrigin: true,
            target: uploadTarget,
            ws: true,
          },
        },
      },
    },
  };
});
