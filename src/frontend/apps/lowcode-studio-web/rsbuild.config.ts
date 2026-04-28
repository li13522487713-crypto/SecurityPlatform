import path from 'node:path';
import { defineConfig } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';

const port = Number(process.env.LOWCODE_STUDIO_PORT || '5183');
const appHostTarget = process.env.APP_HOST_TARGET || 'http://127.0.0.1:5002';

export default defineConfig({
  plugins: [pluginReact()],
  source: {
    entry: { index: './src/main.tsx' },
    define: {
      'process.env.NODE_ENV': JSON.stringify(process.env.NODE_ENV ?? 'development')
    }
  },
  html: { title: 'Atlas Lowcode Studio', template: './index.html' },
  server: {
    port,
    host: '0.0.0.0',
    strictPort: true,
    proxy: [
      // 当前设计态与运行时 API 默认统一走 AppHost（5002）。
      { context: ['/api/v1'], target: appHostTarget, changeOrigin: true },
      { context: ['/api/runtime', '/hubs/lowcode-debug', '/hubs/lowcode-collab', '/hubs/lowcode-preview'], target: appHostTarget, changeOrigin: true }
    ]
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src')
    }
  }
});
