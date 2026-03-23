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
      entry: {
        index: path.resolve(__dirname, "src/index.ts"),
        renderer: path.resolve(__dirname, "src/entry/renderer.ts"),
        designer: path.resolve(__dirname, "src/entry/designer.ts"),
        plugin: path.resolve(__dirname, "src/entry/plugin.ts"),
      },
      name: "AtlasLowCodeUI",
      fileName: (format, entryName) => {
        if (entryName === "index") {
          return `atlas-lowcode-ui.${format}.js`;
        }
        return `atlas-lowcode-ui.${entryName}.${format}.js`;
      },
    },
    rollupOptions: {
      external: [
        "vue",
        "amis",
        "amis-core",
        "amis-editor",
        "amis-formula",
        "amis-ui",
        "amis-theme-editor-helper",
        "i18n-runtime",
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
          "amis-formula": "amisFormula",
          "amis-ui": "amisUi",
          "amis-theme-editor-helper": "amisThemeEditorHelper",
          "i18n-runtime": "i18nRuntime",
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
