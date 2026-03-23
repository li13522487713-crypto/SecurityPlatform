import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * 使用 node_modules 中的 @atlas/lowcode-ui（file:../Atlas.LowCodeUI，指向 dist）。
 * 库产物中含 `import("amis-editor")`，解析需以 demo 根目录的 node_modules 为准（避免从 Atlas.LowCodeUI/dist 相对解析失败）。
 */
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      // 强制统一走 demo 根依赖，避免 file:../Atlas.LowCodeUI 内部 node_modules 参与解析
      amis: path.resolve(__dirname, "node_modules/amis"),
      "amis-core": path.resolve(__dirname, "node_modules/amis-core"),
      "amis-ui": path.resolve(__dirname, "node_modules/amis-ui"),
      "amis-formula": path.resolve(__dirname, "node_modules/amis-formula"),
      "amis-editor": path.resolve(__dirname, "node_modules/amis-editor"),
      "amis-editor-core": path.resolve(__dirname, "node_modules/amis-editor-core"),
      "i18n-runtime": path.resolve(__dirname, "node_modules/i18n-runtime"),
      react: path.resolve(__dirname, "node_modules/react"),
      "react-dom": path.resolve(__dirname, "node_modules/react-dom"),
      "mobx-react": path.resolve(__dirname, "node_modules/mobx-react/dist/mobxreact.esm.js"),
      "mobx-react-lite": path.resolve(__dirname, "node_modules/mobx-react-lite/dist/mobxreactlite.esm.development.js"),
      "prop-types": path.resolve(__dirname, "node_modules/prop-types")
    }
  },
  optimizeDeps: {
    // 显式预构建 amis-editor 及其关键依赖，避免浏览器直接加载 CJS（如 prop-types）导致 default export 报错
    include: ["amis-editor", "amis-editor-core", "mobx-react", "mobx-react-lite", "prop-types"]
  },
  server: {
    host: "0.0.0.0",
    port: 5174
  },
  build: {
    chunkSizeWarningLimit: 10000
  }
});
