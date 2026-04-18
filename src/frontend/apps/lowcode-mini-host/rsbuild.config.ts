import { defineConfig } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';

const port = Number(process.env.LOWCODE_MINI_HOST_PORT || '5187');
const appHostTarget = process.env.APP_HOST_TARGET || 'http://127.0.0.1:5002';

export default defineConfig({
  plugins: [pluginReact()],
  source: { entry: { index: './src/main.tsx' } },
  html: { title: 'Atlas Lowcode Mini Host', template: './index.html' },
  server: {
    port,
    host: '0.0.0.0',
    strictPort: true,
    proxy: [
      { context: ['/api/runtime'], target: appHostTarget, changeOrigin: true }
    ]
  }
});
