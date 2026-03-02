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
    chunkSizeWarningLimit: 2000,
    rollupOptions: {
      external: ["amis-editor"],
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) return undefined;
          const nm = id.replace(/\\/g, "/");
          const pkg = (...names: string[]) =>
            names.some((n) => nm.includes(`/node_modules/${n}/`));

          // --- Heavy standalone libs (amis transitive deps) ---
          if (pkg("monaco-editor")) return "vendor-monaco";
          if (pkg("echarts", "zrender")) return "vendor-echarts";
          if (pkg("pdfjs-dist", "react-pdf")) return "vendor-pdf";
          if (pkg("exceljs", "xlsx", "codepage")) return "vendor-excel";
          if (pkg("tinymce")) return "vendor-tinymce";
          if (pkg("froala-editor", "cropperjs", "react-cropper"))
            return "vendor-richedit";
          if (pkg("hls.js")) return "vendor-hls";
          if (pkg("moment", "moment-timezone")) return "vendor-moment";
          if (pkg("@icons/material")) return "vendor-icons";
          if (pkg("codemirror")) return "vendor-codemirror";

          // --- AMIS ecosystem ---
          if (pkg("amis", "amis-core", "amis-ui", "amis-formula", "office-viewer"))
            return "vendor-amis";

          // --- React ecosystem (all react-* packages together) ---
          if (nm.includes("/node_modules/react")) return "vendor-react";
          if (pkg("scheduler")) return "vendor-react";

          // --- MobX ---
          if (pkg("mobx", "mobx-react-lite")) return "vendor-mobx";

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

          // --- Vue ecosystem ---
          if (
            pkg("vue", "vue-router", "vue-i18n", "vue-demi") ||
            nm.includes("/node_modules/@vue/") ||
            nm.includes("/node_modules/@intlify/")
          )
            return "vendor-vue";

          // --- Remaining (lodash, core-js, downshift, etc.) ---
          return "vendor";
        }
      }
    }
  }
});
