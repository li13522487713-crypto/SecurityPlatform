import path from "node:path";
import { createRequire } from "node:module";
import { defineConfig } from "@coze-arch/rsbuild-config";

const require = createRequire(import.meta.url);
const mode = process.env.ENV_MODE ?? "direct";
const appWebPort = Number(process.env.VITE_APP_WEB_PORT || "5181");
const apiBase = process.env.VITE_API_BASE?.trim();
const derivedAppHostTarget = apiBase && /^https?:\/\//i.test(apiBase)
  ? new URL(apiBase).origin
  : undefined;
const appHostTarget =
  process.env.VITE_APP_HOST_TARGET || derivedAppHostTarget || "http://127.0.0.1:5002";

export default defineConfig({
  server: {
    port: appWebPort,
    host: "0.0.0.0",
    strictPort: true,
    proxy: [
      {
        context: ["/api/v2/workflows", "/api"],
        target: appHostTarget,
        secure: false,
        changeOrigin: true,
      },
    ],
  },
  html: {
    title: "Atlas Application Runtime",
    template: "./index.html",
  },
  source: {
    entry: {
      index: "./src/main.tsx",
    },
    include: [
      path.resolve(__dirname, "../../packages"),
      path.resolve(__dirname, "../../infra"),
      path.resolve(__dirname, "../../config"),
    ],
    alias: {
      "@coze-arch/bot-api$": path.resolve(__dirname, "src/coze-shims/bot-api/index.ts"),
      "@coze-arch/foundation-sdk": require.resolve("@coze-foundation/foundation-sdk"),
      "react-router-dom": require.resolve("react-router-dom"),
    },
    define: {
      "process.env.ENV_MODE": JSON.stringify(mode),
    },
    decorators: {
      version: "legacy",
    },
  },
  output: {
    distPath: {
      root: "dist",
    },
  },
  performance: {
    chunkSplit: {
      strategy: "split-by-size",
      minSize: 3_000_000,
      maxSize: 6_000_000,
    },
  },
  tools: {
    rspack(config, { addRules, mergeConfig }) {
      addRules([
        {
          test: /\.(css|less|jsx|tsx|ts|js)/,
          exclude: [
            new RegExp("apps/app-web/src/app/app.css"),
            /node_modules/,
            new RegExp("packages/arch/i18n"),
          ],
          use: "@coze-arch/import-watch-loader",
        },
      ]);

      return mergeConfig(config, {
        module: {
          parser: {
            javascript: {
              exportsPresence: false,
            },
          },
        },
        resolve: {
          fallback: {
            path: require.resolve("path-browserify"),
          },
        },
        watchOptions: {
          poll: true,
        },
        ignoreWarnings: [
          /Critical dependency: the request of a dependency is an expression/,
          () => true,
        ],
      });
    },
  },
});
