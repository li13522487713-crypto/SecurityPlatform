import { defineConfig } from 'vitest/config';

/**
 * external-connectors-react 单测配置：
 * - jsdom 环境（需要 DOM API 渲染 React 组件）；
 * - 复用 monorepo 根 node_modules 中的 react / @testing-library/react；
 * - 仅扫描本包的 *.test.tsx，避免被 root vitest 拉到的其他包污染；
 * - vitest.setup.ts 内 mock 掉 Semi 间接依赖的 lottie-web / canvas / matchMedia / ResizeObserver。
 */
export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.test.tsx', 'src/**/*.spec.tsx'],
    passWithNoTests: true,
  },
  resolve: {
    dedupe: ['react', 'react-dom'],
  },
});
