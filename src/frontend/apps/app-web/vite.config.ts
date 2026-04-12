import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const semiFoundationRoot = path.resolve(path.dirname(require.resolve("@douyinfe/semi-foundation")), "..", "..", "..");
const lodashRoot = path.dirname(require.resolve("lodash/pick"));

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const appWebPort = Number(env.VITE_APP_WEB_PORT || "5181");
  const appHostTarget = env.VITE_APP_HOST_TARGET || "http://127.0.0.1:5002";

  return {
    logLevel: "error",
    plugins: [react()],
    css: {
      lightningcss: {
        errorRecovery: true
      }
    },
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "src"),
        "@douyinfe/semi-foundation": semiFoundationRoot,
        lodash: lodashRoot
      }
    },
    optimizeDeps: {
      include: ["react", "react-dom", "react/jsx-runtime", "react/jsx-dev-runtime"],
      exclude: [
        "@atlas/app-shell-shared",
        "@atlas/coze-shell-react",
        "@atlas/library-module-react",
        "@atlas/module-admin-react",
        "@atlas/module-explore-react",
        "@atlas/module-studio-react",
        "@atlas/module-workflow-react",
        "@atlas/workflow-core-react"
      ]
    },
    server: {
      host: "0.0.0.0",
      port: appWebPort,
      open: process.env.PLAYWRIGHT_E2E !== "1",
      proxy: {
        "/api/v2/workflows": {
          target: appHostTarget,
          changeOrigin: true,
          secure: false
        },
        "/api": {
          target: appHostTarget,
          changeOrigin: true,
          secure: false
        }
      }
    },
    build: {
      outDir: "dist",
      chunkSizeWarningLimit: 5000
    }
  };
});
