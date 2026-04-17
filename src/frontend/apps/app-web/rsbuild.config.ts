import path from "node:path";
import { createRequire } from "node:module";
import { defineConfig } from "@coze-arch/rsbuild-config";

const require = createRequire(import.meta.url);
const mode = process.env.ENV_MODE ?? "direct";
const isDevelopment = process.env.NODE_ENV !== "production";
const appWebPort = Number(process.env.VITE_APP_WEB_PORT || "5181");
const apiBase = process.env.VITE_API_BASE?.trim();
const derivedAppHostTarget = apiBase && /^https?:\/\//i.test(apiBase)
  ? new URL(apiBase).origin
  : undefined;
const appHostTarget =
  process.env.VITE_APP_HOST_TARGET || derivedAppHostTarget || "http://127.0.0.1:5002";
const workspaceRoots = [
  "../../packages/app-shell-shared",
  "../../packages/atlas-foundation-bridge",
  "../../packages/shared-react-core",
  "../../packages/schema-protocol",
  "../../packages/coze-shell-react",
  "../../packages/library-module-react",
  "../../packages/module-admin-react",
  "../../packages/module-explore-react",
  "../../packages/module-studio-react",
  "../../packages/workflow",
  "../../packages/arch",
  "../../packages/foundation",
  "../../packages/studio",
  "../../packages/common",
  "../../packages/data",
  "../../packages/devops",
  "../../packages/project-ide",
  "../../packages/agent-ide",
].map((relativePath) => path.resolve(__dirname, relativePath));

const importWatchRoots = [
  path.resolve(__dirname, "src"),
  path.resolve(__dirname, "../../packages/app-shell-shared"),
  path.resolve(__dirname, "../../packages/atlas-foundation-bridge"),
  path.resolve(__dirname, "../../packages/shared-react-core"),
  path.resolve(__dirname, "../../packages/coze-shell-react"),
  path.resolve(__dirname, "../../packages/library-module-react"),
  path.resolve(__dirname, "../../packages/module-admin-react"),
  path.resolve(__dirname, "../../packages/module-explore-react"),
  path.resolve(__dirname, "../../packages/module-studio-react"),
  path.resolve(__dirname, "../../packages/workflow"),
  path.resolve(__dirname, "../../packages/arch"),
  path.resolve(__dirname, "../../packages/foundation"),
  path.resolve(__dirname, "../../packages/studio"),
  path.resolve(__dirname, "../../packages/common"),
  path.resolve(__dirname, "../../packages/data"),
  path.resolve(__dirname, "../../packages/devops"),
  path.resolve(__dirname, "../../packages/project-ide"),
  path.resolve(__dirname, "../../packages/agent-ide"),
];

const enablePollingWatch = process.env.ATLAS_RSBUILD_POLL === "true";

export default defineConfig({
  server: {
    port: appWebPort,
    host: "0.0.0.0",
    strictPort: true,
    proxy: [
      {
        // `api/v2/workflows`：v2 为 REST API 版本前缀（后端 `DagWorkflowController`），非产品「V2」语义
        context: ["/api/v2/workflows", "/api", "/v1"],
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
      ...workspaceRoots,
      path.resolve(__dirname, "../../infra"),
      path.resolve(__dirname, "../../config"),
    ],
    alias: {
      "react$": require.resolve("react"),
      "react-dom$": require.resolve("react-dom"),
      "react/jsx-runtime": require.resolve("react/jsx-runtime"),
      "react/jsx-dev-runtime": require.resolve("react/jsx-dev-runtime"),
      "@coze-arch/bot-api$": path.resolve(__dirname, "src/coze-shims/bot-api/index.ts"),
      "@coze-arch/bot-api/developer_api$": path.resolve(__dirname, "src/coze-shims/bot-api/developer_api.ts"),
      "@coze-arch/bot-api/intelligence_api$": path.resolve(__dirname, "src/coze-shims/bot-api/intelligence_api.ts"),
      "@coze-arch/bot-api/playground_api$": path.resolve(__dirname, "src/coze-shims/bot-api/playground_api.ts"),
      "@coze-arch/bot-api/workflow_api$": path.resolve(__dirname, "src/coze-shims/bot-api/workflow_api.ts"),
      "@coze-arch/foundation-sdk": require.resolve("@atlas/foundation-bridge"),
      "@coze-foundation/foundation-sdk": require.resolve("@atlas/foundation-bridge"),
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
    // Use cheaper source maps in dev to reduce first compile time on large workspace graphs.
    // dev: 同步关闭 css source-map 以避免 250+ packages 下重复构建 css map 的开销；
    // 生产环境继续保留以便线上排错。
    sourceMap: {
      js: isDevelopment ? false : "source-map",
      css: !isDevelopment,
    },
  },
  ...(isDevelopment
    ? {}
    : {
        performance: {
          chunkSplit: {
            strategy: "split-by-size",
            minSize: 3_000_000,
            maxSize: 6_000_000,
          },
        },
      }),
  tools: {
    rspack(config, { addRules, mergeConfig }) {
      if (!isDevelopment) {
        addRules([
          {
            test: /\.(css|less|jsx|tsx|ts|js)/,
            include: importWatchRoots,
            exclude: [
              new RegExp("apps/app-web/src/app/app.css"),
              /node_modules/,
              new RegExp("packages/arch/i18n"),
            ],
            use: "@coze-arch/import-watch-loader",
          },
        ]);
      }

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
          poll: enablePollingWatch,
        },
        ignoreWarnings: [
          /Critical dependency: the request of a dependency is an expression/,
        ],
      });
    },
  },
});
