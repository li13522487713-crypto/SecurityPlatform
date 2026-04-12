const BENIGN_BROWSER_WARNINGS = [
  "findDOMNode is deprecated and will be removed in the next major release.",
  "ResizeObserver loop completed with undelivered notifications.",
  "ResizeObserver loop limit exceeded"
];

declare global {
  interface Window {
    __atlasAppConsolePatched__?: boolean;
  }
}

function isBenignBrowserWarning(message: unknown): boolean {
  return (
    typeof message === "string" &&
    BENIGN_BROWSER_WARNINGS.some((item) => message.includes(item))
  );
}

export function suppressBenignBrowserErrors() {
  if (typeof window === "undefined" || window.__atlasAppConsolePatched__) {
    return;
  }

  const methods: Array<"warn" | "error"> = ["warn", "error"];
  for (const method of methods) {
    const original = window.console[method];
    window.console[method] = ((...args: unknown[]) => {
      const message = args
        .map((item) => {
          if (typeof item === "string") {
            return item;
          }

          if (item instanceof Error) {
            return item.message;
          }

          return "";
        })
        .join(" ");

      if (isBenignBrowserWarning(message)) {
        return;
      }

      original(...args);
    }) as Console[typeof method];
  }

  window.addEventListener("error", (event) => {
    if (isBenignBrowserWarning(event.message) || isBenignBrowserWarning(event.error?.message)) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  });

  window.addEventListener("unhandledrejection", (event) => {
    const reason =
      typeof event.reason === "string"
        ? event.reason
        : event.reason instanceof Error
          ? event.reason.message
          : "";

    if (isBenignBrowserWarning(reason)) {
      event.preventDefault();
    }
  });

  window.__atlasAppConsolePatched__ = true;
}
