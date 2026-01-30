import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import path from "node:path";

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src")
    }
  },
  server: {
    host: "0.0.0.0",
    port: 5173,
    open: true,
    proxy: {
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false,
        // 后端 Controller 路由不带 /api 前缀，这里需要重写去掉 /api
        rewrite: (path) => path.replace(/^\/api/, "")
      }
    }
  },
  build: {
    chunkSizeWarningLimit: 2000,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) {
            return undefined;
          }
          if (id.includes("vform3-builds")) {
            return "vendor-vform";
          }
          if (id.includes("@antv/x6")) {
            return "vendor-x6";
          }
          if (id.includes("ant-design-vue") || id.includes("@ant-design")) {
            return "vendor-antd";
          }
          if (id.includes("element-plus")) {
            return "vendor-element";
          }
          if (id.includes("vue-router")) {
            return "vendor-router";
          }
          return "vendor";
        }
      }
    }
  }
});
