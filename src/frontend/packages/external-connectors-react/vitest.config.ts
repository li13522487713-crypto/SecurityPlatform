import { defineConfig } from 'vitest/config';

/**
 * external-connectors-react 单测配置：
 * - jsdom 环境（需要 DOM API 渲染 React 组件）；
 * - 复用 monorepo 根 node_modules 中的 react / @testing-library/react；
 * - 仅扫描本包的 *.test.tsx，避免被 root vitest 拉到的其他包污染。
 */
export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    include: ['src/**/*.test.tsx', 'src/**/*.spec.tsx'],
    passWithNoTests: true,
  },
  resolve: {
    // 让 react / react-dom 走 monorepo 根 node_modules，避免 ESM 双实例。
    dedupe: ['react', 'react-dom'],
  },
});
