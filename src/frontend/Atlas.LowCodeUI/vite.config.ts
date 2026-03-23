import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";
import dts from "vite-plugin-dts";
import path from "node:path";

export default defineConfig({
  plugins: [
    vue(),
    dts({
      insertTypesEntry: true,
      outDir: "dist",
      tsconfigPath: "./tsconfig.json",
    }),
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
    },
  },
  build: {
    lib: {
      entry: path.resolve(__dirname, "src/index.ts"),
      name: "AtlasLowCodeUI",
      fileName: (format) => `atlas-lowcode-ui.${format}.js`,
    },
    rollupOptions: {
      external: [
        "vue",
        "amis",
        "amis-core",
        "amis-editor",
        "amis-ui",
        "react",
        "react-dom",
        "react-dom/client",
        "react/jsx-runtime",
        "@guolao/vue-monaco-editor",
      ],
      output: {
        exports: "named",
        globals: {
          vue: "Vue",
          amis: "amis",
          "amis-core": "amisCore",
          "amis-editor": "amisEditor",
          "amis-ui": "amisUi",
          react: "React",
          "react-dom": "ReactDOM",
          "react-dom/client": "ReactDOMClient",
          "react/jsx-runtime": "jsxRuntime",
          "@guolao/vue-monaco-editor": "VueMonacoEditor",
        },
      },
    },
    cssCodeSplit: false,
  },
});
