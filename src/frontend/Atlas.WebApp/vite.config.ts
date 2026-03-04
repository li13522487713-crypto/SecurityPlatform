import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
      "@popperjs/core/lib/modifiers/arrow": path.resolve(__dirname, "src/compat/popper/modifiers/arrow.ts"),
      "@popperjs/core/lib/modifiers/computeStyles": path.resolve(
        __dirname,
        "src/compat/popper/modifiers/computeStyles.ts"
      ),
      "@popperjs/core/lib/modifiers/eventListeners": path.resolve(
        __dirname,
        "src/compat/popper/modifiers/eventListeners.ts"
      ),
      "@popperjs/core/lib/modifiers/flip": path.resolve(__dirname, "src/compat/popper/modifiers/flip.ts"),
      "@popperjs/core/lib/modifiers/hide": path.resolve(__dirname, "src/compat/popper/modifiers/hide.ts"),
      "@popperjs/core/lib/modifiers/offset": path.resolve(__dirname, "src/compat/popper/modifiers/offset.ts"),
      "@popperjs/core/lib/modifiers/popperOffsets": path.resolve(
        __dirname,
        "src/compat/popper/modifiers/popperOffsets.ts"
      ),
      "@popperjs/core/lib/modifiers/preventOverflow": path.resolve(
        __dirname,
        "src/compat/popper/modifiers/preventOverflow.ts"
      ),
      "@popperjs/core/lib/enums": path.resolve(__dirname, "src/compat/popper/enums.ts"),
      "@popperjs/core/lib/popper-base": path.resolve(__dirname, "src/compat/popper/popper-base.ts")
    }
  },
  server: {
    host: "0.0.0.0",
    port: 5173,
    open: true,
    proxy: {
      "/api/v1": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false
      }
    }
  },
  build: {
    chunkSizeWarningLimit: 8000,
    rollupOptions: {
      external: ["amis-editor"],
      onwarn(warning, warn) {
        const warningMessage = warning.message ?? "";
        if (
          warning.code === "MODULE_LEVEL_DIRECTIVE" &&
          warningMessage.includes('"use client"') &&
          (warning.id?.includes("/node_modules/react-pdf/") ||
            warning.id?.includes("/node_modules/react-intersection-observer/"))
        ) {
          return;
        }

        if (
          warning.code === "EVAL" &&
          (warning.id?.includes("/node_modules/amis/") ||
            warning.id?.includes("/node_modules/vform3-builds/"))
        ) {
          return;
        }

        warn(warning);
      },
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) return undefined;
          const nm = id.replace(/\\/g, "/");
          const pkg = (...names: string[]) =>
            names.some((n) => nm.includes(`/node_modules/${n}/`));

          // --- Heavy standalone libs (amis transitive deps) ---
          if (pkg("monaco-editor")) return "vendor-monaco";
          if (pkg("echarts", "zrender")) return "vendor-echarts";
          if (pkg("exceljs", "xlsx", "codepage")) return "vendor-excel";
          if (pkg("tinymce")) return "vendor-tinymce";
          if (pkg("hls.js")) return "vendor-hls";
          if (pkg("moment", "moment-timezone")) return "vendor-moment";
          if (pkg("codemirror")) return "vendor-codemirror";

          // --- AMIS ecosystem（合并其 React/MobX 依赖，避免循环 chunk） ---
          if (
            pkg(
              "amis",
              "amis-core",
              "amis-ui",
              "amis-formula",
              "office-viewer",
              "pdfjs-dist",
              "react-pdf",
              "froala-editor",
              "cropperjs",
              "react-cropper",
              "react",
              "react-dom",
              "react-is",
              "scheduler",
              "mobx",
              "mobx-react-lite",
              "@icons/material"
            )
          )
            return "vendor-amis";

          // --- VForm ---
          if (pkg("vform3-builds")) return "vendor-vform";

          // --- AntV X6 ---
          if (nm.includes("/node_modules/@antv/")) return "vendor-x6";

          // --- Ant Design Vue ---
          if (pkg("ant-design-vue") || nm.includes("/node_modules/@ant-design/"))
            return "vendor-antd";

          // --- Element Plus ---
          if (pkg("element-plus") || nm.includes("/node_modules/@element-plus/"))
            return "vendor-element";

          // 其余依赖交给 Rollup 默认策略，减少人为拆包导致的循环依赖。
          return undefined;
        }
      }
    }
  }
});
