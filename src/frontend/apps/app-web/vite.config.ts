import { defineConfig, loadEnv } from "vite";
import vue from "@vitejs/plugin-vue";
import react from "@vitejs/plugin-react";
import path from "node:path";

function normalizeRuntimeMode(rawMode: string | undefined, mode: string): "platform" | "direct" {
  const fallback = mode === "direct" ? "direct" : "platform";
  const normalized = String(rawMode ?? fallback).trim().toLowerCase();
  return normalized === "direct" ? "direct" : "platform";
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const runtimeMode = normalizeRuntimeMode(env.VITE_APP_RUNTIME_MODE, mode);
  const appWebPort = runtimeMode === "direct" ? 5182 : 5181;
  const platformHostTarget = env.VITE_PLATFORM_HOST_TARGET || "http://127.0.0.1:5001";
  const appHostTarget = env.VITE_APP_HOST_TARGET || "http://127.0.0.1:5002";
  const apiTarget = runtimeMode === "direct" ? appHostTarget : platformHostTarget;

  return {
    logLevel: "error",
    plugins: [vue(), react()],
    css: {
      lightningcss: {
        errorRecovery: true
      }
    },
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "src")
      }
    },
    server: {
      host: "0.0.0.0",
      port: appWebPort,
      open: process.env.PLAYWRIGHT_E2E !== "1",
      proxy: {
        "/api/v1/setup": {
          target: appHostTarget,
          changeOrigin: true,
          secure: false
        },
        "/api/v2/workflows": {
          target: appHostTarget,
          changeOrigin: true,
          secure: false
        },
        "/api": {
          target: apiTarget,
          changeOrigin: true,
          secure: false
        },
        "/app-host": {
          target: appHostTarget,
          changeOrigin: true,
          secure: false,
          rewrite: (path) => path.replace(/^\/app-host\/[^/]+/, "")
        }
      }
    },
    build: {
      outDir: "dist",
      chunkSizeWarningLimit: 5000
    }
  };
});
