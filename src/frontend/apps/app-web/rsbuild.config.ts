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
const workspaceRoots = [
  "../../packages/app-shell-shared",
  "../../packages/shared-react-core",
  "../../packages/schema-protocol",
  "../../packages/coze-shell-react",
  "../../packages/library-module-react",
  "../../packages/module-admin-react",
  "../../packages/module-explore-react",
  "../../packages/module-studio-react",
  "../../packages/module-workflow-react",
  "../../packages/workflow-core-react",
  "../../packages/workflow-editor-react",
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
  path.resolve(__dirname, "../../packages/shared-react-core"),
  path.resolve(__dirname, "../../packages/coze-shell-react"),
  path.resolve(__dirname, "../../packages/library-module-react"),
  path.resolve(__dirname, "../../packages/module-admin-react"),
  path.resolve(__dirname, "../../packages/module-explore-react"),
  path.resolve(__dirname, "../../packages/module-studio-react"),
  path.resolve(__dirname, "../../packages/module-workflow-react"),
  path.resolve(__dirname, "../../packages/workflow-core-react"),
  path.resolve(__dirname, "../../packages/workflow-editor-react"),
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
          include: importWatchRoots,
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
          poll: enablePollingWatch,
        },
        ignoreWarnings: [
          /Critical dependency: the request of a dependency is an expression/,
        ],
      });
    },
  },
});
