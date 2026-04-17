import { defineConfig } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';

const port = Number(process.env.LOWCODE_PREVIEW_PORT || '5184');
const appHostTarget = process.env.APP_HOST_TARGET || 'http://127.0.0.1:5002';

export default defineConfig({
  plugins: [pluginReact()],
  source: { entry: { index: './src/main.tsx' } },
  html: { title: 'Atlas Lowcode Preview', template: './index.html' },
  server: {
    port,
    host: '0.0.0.0',
    strictPort: true,
    proxy: [
      { context: ['/api/runtime', '/hubs/lowcode-preview', '/hubs/lowcode-debug'], target: appHostTarget, changeOrigin: true, ws: true }
    ]
  }
});
