import { defineConfig } from "vitest/config";
import { resolve } from "node:path";

export default defineConfig({
  resolve: {
    alias: {
      "@douyinfe/semi-icons": resolve(__dirname, "src/test/semi-icons-stub.ts"),
    },
  },
  test: {
    environment: "node",
  },
});
