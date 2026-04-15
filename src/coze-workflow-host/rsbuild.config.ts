import path from "path";
import { defineConfig } from "@coze-arch/rsbuild-config";
import { GLOBAL_ENVS } from "@coze-arch/bot-env";

const apiTarget = process.env.WEB_SERVER_PORT
  ? `http://localhost:${process.env.WEB_SERVER_PORT}/`
  : "http://localhost:5002/";

export default defineConfig({
  server: {
    strictPort: true,
    proxy: [
      {
        context: ["/api"],
        target: apiTarget,
        secure: false,
        changeOrigin: true
      },
      {
        context: ["/v1"],
        target: apiTarget,
        secure: false,
        changeOrigin: true
      }
    ]
  },
  html: {
    title: "Atlas Coze Workflow Host",
    template: "./index.html",
    crossorigin: "anonymous"
  },
  tools: {
    postcss: (_opts, { addPlugins }) => {
      addPlugins([
        // eslint-disable-next-line @typescript-eslint/no-require-imports
        require("tailwindcss")({
          content: ["./src/**/*.{ts,tsx}", "../frontend/packages/**/*.{ts,tsx}"]
        })
      ]);
    },
    rspack(config, { addRules, mergeConfig }) {
      addRules([
        {
          test: /\.(css|less|jsx|tsx|ts|js)$/,
          exclude: [/node_modules/],
          use: "@coze-arch/import-watch-loader"
        }
      ]);

      return mergeConfig(config, {
        watchOptions: {
          poll: true
        }
      });
    }
  },
  source: {
    define: {
      "process.env.IS_REACT18": JSON.stringify(true),
      "process.env.RUNTIME_ENTRY": JSON.stringify("@coze-dev/runtime"),
      "process.env.TARO_ENV": JSON.stringify("h5"),
      ENABLE_COVERAGE: JSON.stringify(false)
    },
    include: [
      path.resolve(__dirname, "../frontend/packages"),
      path.resolve(__dirname, "../frontend/infra"),
      /\/node_modules\/(marked|@dagrejs|@tanstack)\//
    ],
    alias: {
      "@coze-arch/foundation-sdk": require.resolve("@coze-foundation/foundation-sdk")
    },
    decorators: {
      version: "legacy"
    }
  },
  output: {
    distPath: {
      root: "dist"
    }
  }
});

void GLOBAL_ENVS;
