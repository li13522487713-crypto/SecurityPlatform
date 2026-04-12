const RESIZE_OBSERVER_MESSAGES = [
  "ResizeObserver loop completed with undelivered notifications.",
  "ResizeObserver loop limit exceeded"
];

declare global {
  interface Window {
    __atlasResizeObserverPatched__?: boolean;
    __atlasResizeObserverConsolePatched__?: boolean;
  }
}

function isBenignResizeObserverMessage(message: unknown): boolean {
  return typeof message === "string" && RESIZE_OBSERVER_MESSAGES.some((item) => message.includes(item));
}

function patchResizeObserver(): void {
  if (typeof window === "undefined" || window.__atlasResizeObserverPatched__) {
    return;
  }

  const NativeResizeObserver = window.ResizeObserver;
  if (typeof NativeResizeObserver !== "function") {
    return;
  }

  class DeferredResizeObserver implements ResizeObserver {
    private readonly observer: ResizeObserver;
    private readonly callback: ResizeObserverCallback;
    private frameId: number | null = null;
    private queuedEntries: ResizeObserverEntry[] = [];

    constructor(callback: ResizeObserverCallback) {
      this.callback = callback;
      this.observer = new NativeResizeObserver((entries, observer) => {
        this.queuedEntries = entries;
        if (this.frameId !== null) {
          window.cancelAnimationFrame(this.frameId);
        }
        this.frameId = window.requestAnimationFrame(() => {
          this.frameId = null;
          this.callback(this.queuedEntries, observer);
        });
      });
    }

    disconnect(): void {
      if (this.frameId !== null) {
        window.cancelAnimationFrame(this.frameId);
        this.frameId = null;
      }
      this.observer.disconnect();
    }

    observe(target: Element, options?: ResizeObserverOptions): void {
      this.observer.observe(target, options);
    }

    takeRecords(): ResizeObserverEntry[] {
      return this.observer.takeRecords();
    }

    unobserve(target: Element): void {
      this.observer.unobserve(target);
    }
  }

  window.ResizeObserver = DeferredResizeObserver as typeof ResizeObserver;
  window.__atlasResizeObserverPatched__ = true;
}

function patchConsole(): void {
  if (typeof window === "undefined" || window.__atlasResizeObserverConsolePatched__) {
    return;
  }

  const methods: Array<"error" | "warn"> = ["error", "warn"];
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

      if (isBenignResizeObserverMessage(message)) {
        return;
      }

      original(...args);
    }) as Console[typeof method];
  }

  window.__atlasResizeObserverConsolePatched__ = true;
}

export function suppressBenignBrowserErrors(): void {
  if (typeof window === "undefined") {
    return;
  }

  patchResizeObserver();
  patchConsole();

  window.addEventListener("error", (event) => {
    if (isBenignResizeObserverMessage(event.message) || isBenignResizeObserverMessage(event.error?.message)) {
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
    if (isBenignResizeObserverMessage(reason)) {
      event.preventDefault();
    }
  });
}
