/**
 * lowcode-web-sdk 库模式构建（M17 P2-1）。
 *
 * 双输出：UMD + ESM；UMD 提供 <script> 嵌入用 globalName=AtlasLowcode；
 * ESM 提供 npm 导入用。CDN 路径建议：
 *   https://cdn.atlas.local/lowcode-sdk/v{version}/atlas-lowcode.umd.js
 *   https://cdn.atlas.local/lowcode-sdk/v{version}/atlas-lowcode.esm.js
 */
import { defineConfig } from '@rsbuild/core';

export default defineConfig({
  source: {
    entry: { 'atlas-lowcode': './src/index.ts' }
  },
  output: {
    target: 'web',
    distPath: { root: 'dist' },
    cleanDistPath: true,
    sourceMap: { js: 'source-map' }
  },
  lib: [
    {
      format: 'umd',
      umdName: 'AtlasLowcode',
      output: { distPath: { root: 'dist' } }
    },
    {
      format: 'esm',
      output: { distPath: { root: 'dist' } }
    }
  ],
  performance: {
    chunkSplit: { strategy: 'all-in-one' }
  }
});
