import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app";

const runtime = globalThis as Record<string, boolean | string | undefined>;
runtime.IS_BOT_OP = runtime.IS_BOT_OP ?? false;
runtime.IS_OPEN_SOURCE = runtime.IS_OPEN_SOURCE ?? true;
runtime.IS_OVERSEA = runtime.IS_OVERSEA ?? false;

const container = document.getElementById("app");

if (!container) {
  throw new Error("App container '#app' was not found.");
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>
);
