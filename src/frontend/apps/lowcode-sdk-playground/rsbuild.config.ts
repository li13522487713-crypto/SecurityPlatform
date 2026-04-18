import { defineConfig } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';

const port = Number(process.env.LOWCODE_SDK_PLAYGROUND_PORT || '5186');

export default defineConfig({
  plugins: [pluginReact()],
  source: { entry: { index: './src/main.tsx' } },
  html: { title: 'Atlas Lowcode SDK Playground', template: './index.html' },
  server: { port, host: '0.0.0.0', strictPort: true }
});
