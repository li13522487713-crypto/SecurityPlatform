import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const lowcodeUiDist = path.resolve(__dirname, "../Atlas.LowCodeUI/dist");
const mobxReactEsm = path.resolve(__dirname, "node_modules/amis-editor-core/node_modules/mobx-react/dist/mobxreact.esm.js");
const mobxReactLiteEsm = path.resolve(__dirname, "node_modules/amis-editor-core/node_modules/mobx-react/node_modules/mobx-react-lite/es/index.js");

/**
 * Demo 直接消费兄弟库 dist 产物，避免 file: 依赖复制出一份带 node_modules 的本地包，
 * 导致 amis / react / mobx 解析到多套版本。
 */
export default defineConfig({
  plugins: [vue()],
  resolve: {
    dedupe: [
      "react",
      "react-dom",
      "mobx",
      "mobx-react",
      "mobx-react-lite",
      "amis",
      "amis-core",
      "amis-ui",
      "amis-formula",
      "amis-editor",
      "amis-editor-core",
      "i18n-runtime"
    ],
    alias: {
      "@atlas/lowcode-ui/style.css": path.resolve(lowcodeUiDist, "lowcode-ui.css"),
      "@atlas/lowcode-ui/designer": path.resolve(lowcodeUiDist, "atlas-lowcode-ui.designer.es.js"),
      "@atlas/lowcode-ui/renderer": path.resolve(lowcodeUiDist, "atlas-lowcode-ui.renderer.es.js"),
      "@atlas/lowcode-ui/plugin": path.resolve(lowcodeUiDist, "atlas-lowcode-ui.plugin.es.js"),
      "@atlas/lowcode-ui": path.resolve(lowcodeUiDist, "atlas-lowcode-ui.es.js"),
      "react-frame-component": path.resolve(__dirname, "src/shims/react-frame-component.ts"),
      "mobx-react": mobxReactEsm,
      "mobx-react-lite": mobxReactLiteEsm
    }
  },
  optimizeDeps: {
    // 额外预构建 React runtime，避免其作为 CJS 片段被内联进 amis-editor 依赖块后残留 require("react")
    include: ["amis-editor", "amis-editor-core", "react", "react-dom", "react/jsx-runtime", "react/jsx-dev-runtime"],
    needsInterop: ["react", "react-dom", "react/jsx-runtime", "react/jsx-dev-runtime"]
  },
  server: {
    host: "0.0.0.0",
    port: 5174,
    fs: {
      allow: [__dirname, lowcodeUiDist]
    }
  },
  build: {
    chunkSizeWarningLimit: 10000
  }
});
