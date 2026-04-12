import {
  expect,
  test as base,
  type BrowserContext,
  type Locator,
  type Page
} from "@playwright/test";
import { appBaseUrl, loginApp, platformBaseUrl } from "../app/helpers";

type SessionFixtures = {
  _sharedContext: BrowserContext;
  _sharedPage: Page;
  ensureLoggedInSession: (appKey: string) => Promise<void>;
  resetAuthForCase: () => Promise<void>;
};

let createdContextCount = 0;
let createdPageCount = 0;
let sharedPage: Page | null = null;
let activeAppSessionKey: string | null = null;
const appProfileStorageKey = "atlas_app_auth_profile";
const appAccessTokenStorageKey = "atlas_app_access_token";
const appTenantStorageKey = "atlas_app_tenant_id";
const mousePositionMap = new WeakMap<Page, { x: number; y: number }>();
const decoratedHandles = new WeakSet<object>();
const rawClickSymbol = Symbol("raw-click");
const rawDoubleClickSymbol = Symbol("raw-double-click");
const rawHoverSymbol = Symbol("raw-hover");

type HumanClickOptions = {
  button?: "left" | "middle" | "right";
  clickCount?: number;
  delay?: number;
  force?: boolean;
  modifiers?: ("Alt" | "Control" | "ControlOrMeta" | "Meta" | "Shift")[];
  noWaitAfter?: boolean;
  position?: { x: number; y: number };
  timeout?: number;
  trial?: boolean;
};

type HumanHoverOptions = {
  force?: boolean;
  modifiers?: ("Alt" | "Control" | "ControlOrMeta" | "Meta" | "Shift")[];
  noWaitAfter?: boolean;
  position?: { x: number; y: number };
  timeout?: number;
  trial?: boolean;
};

function randomBetween(min: number, max: number) {
  return min + Math.random() * (max - min);
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

async function moveMouseHumanLike(page: Page, destination: { x: number; y: number }) {
  const viewport = page.viewportSize() ?? { width: 1440, height: 900 };
  const start =
    mousePositionMap.get(page) ?? {
      x: randomBetween(24, Math.max(48, viewport.width * 0.18)),
      y: randomBetween(24, Math.max(48, viewport.height * 0.16))
    };

  const deltaX = destination.x - start.x;
  const deltaY = destination.y - start.y;
  const distance = Math.hypot(deltaX, deltaY);
  const steps = Math.max(16, Math.min(36, Math.round(distance / 20)));
  let currentX = start.x;
  let currentY = start.y;

  for (let step = 1; step <= steps; step += 1) {
    const progress = step / steps;
    const easing = progress < 0.5
      ? 2 * progress * progress
      : 1 - Math.pow(-2 * progress + 2, 2) / 2;
    const jitterScale = (1 - progress) * 6;
    currentX = start.x + deltaX * easing + randomBetween(-jitterScale, jitterScale);
    currentY = start.y + deltaY * easing + randomBetween(-jitterScale, jitterScale);
    await page.mouse.move(currentX, currentY, { steps: 1 });
    await page.waitForTimeout(randomBetween(14, 30));
  }

  await page.mouse.move(destination.x, destination.y, { steps: 1 });
  mousePositionMap.set(page, destination);
}

async function humanClick(page: Page, locator: Locator, options?: HumanClickOptions): Promise<void> {
  const rawClick = (
    (locator as Record<PropertyKey, unknown>)[rawClickSymbol] as
      | ((options?: HumanClickOptions) => Promise<void>)
      | undefined
  )?.bind(locator);
  const {
    button = "left",
    clickCount = 1,
    delay = 0,
    force = false,
    modifiers,
    noWaitAfter,
    position,
    timeout,
    trial
  } = options ?? {};

  if (trial) {
    if (rawClick) {
      await rawClick({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout, trial: true });
      return;
    }

    await locator.click({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout, trial: true });
    return;
  }

  if (rawClick) {
    await rawClick({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout, trial: true });
  } else {
    await locator.click({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout, trial: true });
  }
  await locator.scrollIntoViewIfNeeded();

  const box = await locator.boundingBox();
  if (!box) {
    if (rawClick) {
      await rawClick({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout });
    } else {
      await locator.click({ button, clickCount, delay, force, modifiers, noWaitAfter, position, timeout });
    }
    return;
  }

  const horizontalPadding = Math.min(18, Math.max(6, box.width * 0.18));
  const verticalPadding = Math.min(16, Math.max(6, box.height * 0.18));
  const destination = {
    x: clamp(
      box.x + (position?.x ?? randomBetween(horizontalPadding, Math.max(horizontalPadding, box.width - horizontalPadding))),
      box.x + 1,
      box.x + Math.max(1, box.width - 1)
    ),
    y: clamp(
      box.y + (position?.y ?? randomBetween(verticalPadding, Math.max(verticalPadding, box.height - verticalPadding))),
      box.y + 1,
      box.y + Math.max(1, box.height - 1)
    )
  };

  await moveMouseHumanLike(page, destination);
  await page.waitForTimeout(randomBetween(32, 86));

  for (const modifier of modifiers ?? []) {
    await page.keyboard.down(modifier);
  }

  for (let index = 0; index < clickCount; index += 1) {
    await page.mouse.down({ button });
    await page.waitForTimeout(delay > 0 ? delay : randomBetween(36, 92));
    await page.mouse.up({ button });
    if (index < clickCount - 1) {
      await page.waitForTimeout(randomBetween(54, 128));
    }
  }

  if (modifiers && modifiers.length > 0) {
    for (const modifier of modifiers) {
      await page.keyboard.up(modifier);
    }
  }

  if (!noWaitAfter) {
    await page.waitForTimeout(randomBetween(18, 48));
  }
}

async function humanHover(page: Page, locator: Locator, options?: HumanHoverOptions): Promise<void> {
  const rawHover = (
    (locator as Record<PropertyKey, unknown>)[rawHoverSymbol] as
      | ((options?: HumanHoverOptions) => Promise<void>)
      | undefined
  )?.bind(locator);
  const {
    force = false,
    modifiers,
    noWaitAfter,
    position,
    timeout,
    trial
  } = options ?? {};

  if (trial) {
    if (rawHover) {
      await rawHover({ force, modifiers, noWaitAfter, position, timeout, trial: true });
      return;
    }

    await locator.hover({ force, modifiers, noWaitAfter, position, timeout, trial: true });
    return;
  }

  if (rawHover) {
    await rawHover({ force, modifiers, noWaitAfter, position, timeout, trial: true });
  } else {
    await locator.hover({ force, modifiers, noWaitAfter, position, timeout, trial: true });
  }
  await locator.scrollIntoViewIfNeeded();

  const box = await locator.boundingBox();
  if (!box) {
    if (rawHover) {
      await rawHover({ force, modifiers, noWaitAfter, position, timeout });
    } else {
      await locator.hover({ force, modifiers, noWaitAfter, position, timeout });
    }
    return;
  }

  const horizontalPadding = Math.min(18, Math.max(6, box.width * 0.18));
  const verticalPadding = Math.min(16, Math.max(6, box.height * 0.18));
  const destination = {
    x: clamp(
      box.x + (position?.x ?? randomBetween(horizontalPadding, Math.max(horizontalPadding, box.width - horizontalPadding))),
      box.x + 1,
      box.x + Math.max(1, box.width - 1)
    ),
    y: clamp(
      box.y + (position?.y ?? randomBetween(verticalPadding, Math.max(verticalPadding, box.height - verticalPadding))),
      box.y + 1,
      box.y + Math.max(1, box.height - 1)
    )
  };

  for (const modifier of modifiers ?? []) {
    await page.keyboard.down(modifier);
  }

  await moveMouseHumanLike(page, destination);
    await page.waitForTimeout(randomBetween(34, 88));

  if (modifiers && modifiers.length > 0) {
    for (const modifier of modifiers) {
      await page.keyboard.up(modifier);
    }
  }

  if (!noWaitAfter) {
    await page.waitForTimeout(randomBetween(22, 48));
  }
}

function isWrappableHandle(value: unknown): value is Record<string, unknown> {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  if (typeof (value as { then?: unknown }).then === "function") {
    return false;
  }

  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.click === "function" ||
    typeof candidate.locator === "function" ||
    typeof candidate.getByTestId === "function" ||
    typeof candidate.getByRole === "function"
  );
}

function wrapHandleMethod<T extends object>(
  target: T,
  methodName: string,
  originalStorageKey: symbol | null,
  wrapper: (original: (...args: unknown[]) => unknown, args: unknown[]) => unknown
) {
  const candidate = (target as Record<string, unknown>)[methodName];
  if (typeof candidate !== "function") {
    return;
  }

  const original = candidate.bind(target);
  if (originalStorageKey) {
    Object.defineProperty(target, originalStorageKey, {
      configurable: true,
      value: original
    });
  }
  Object.defineProperty(target, methodName, {
    configurable: true,
    writable: true,
    value: (...args: unknown[]) => wrapper(original, args)
  });
}

function decorateInteractiveHandle<T extends object>(target: T, page: Page): T {
  if (decoratedHandles.has(target)) {
    return target;
  }

  decoratedHandles.add(target);

  wrapHandleMethod(target, "click", rawClickSymbol, (_original, args) =>
    humanClick(page, target as unknown as Locator, args[0] as HumanClickOptions | undefined)
  );
  wrapHandleMethod(target, "dblclick", rawDoubleClickSymbol, (_original, args) =>
    humanClick(page, target as unknown as Locator, {
      ...(args[0] as HumanClickOptions | undefined),
      clickCount: 2
    })
  );
  wrapHandleMethod(target, "hover", rawHoverSymbol, (_original, args) =>
    humanHover(page, target as unknown as Locator, args[0] as HumanHoverOptions | undefined)
  );

  for (const methodName of [
    "locator",
    "getByAltText",
    "getByLabel",
    "getByPlaceholder",
    "getByRole",
    "getByTestId",
    "getByText",
    "getByTitle",
    "filter",
    "first",
    "last",
    "nth",
    "and",
    "or"
  ]) {
    wrapHandleMethod(target, methodName, null, (original, args) => {
      const result = original(...args);
      return isWrappableHandle(result) ? decorateInteractiveHandle(result, page) : result;
    });
  }

  return target;
}

function createHumanLikePage(page: Page): Page {
  return decorateInteractiveHandle(page, page);
}

const initResizeObserverSuppression = () => {
  const resizeObserverMessages = [
    "ResizeObserver loop completed with undelivered notifications.",
    "ResizeObserver loop limit exceeded"
  ];

  const isBenignResizeObserverMessage = (message: unknown): boolean =>
    typeof message === "string" && resizeObserverMessages.some((item) => message.includes(item));

  const patchConsoleMethod = (method: "error" | "warn") => {
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
  };

  if (typeof window.ResizeObserver === "function") {
    const NativeResizeObserver = window.ResizeObserver;
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
  }

  patchConsoleMethod("error");
  patchConsoleMethod("warn");

  const installHumanCursorOverlay = () => {
    if (document.getElementById("atlas-e2e-human-cursor-style")) {
      return;
    }

    const style = document.createElement("style");
    style.id = "atlas-e2e-human-cursor-style";
    style.textContent = `
      #atlas-e2e-human-cursor {
        position: fixed;
        left: 0;
        top: 0;
        z-index: 2147483647;
        pointer-events: none;
        transform: translate(-50%, -50%);
        transition: transform 90ms ease, opacity 120ms ease;
        opacity: 0;
        font-size: 15px;
        line-height: 1;
        text-shadow: 0 2px 6px rgba(15, 23, 42, 0.24);
        will-change: transform, opacity;
      }
      #atlas-e2e-human-cursor.atlas-e2e-human-cursor-visible {
        opacity: 1;
      }
      #atlas-e2e-human-cursor.atlas-e2e-human-cursor-pointer {
        transform: translate(-46%, -46%) scale(1.06);
      }
      .atlas-e2e-human-click-ripple {
        position: fixed;
        width: 16px;
        height: 16px;
        border: 2px solid rgba(37, 99, 235, 0.55);
        border-radius: 999px;
        pointer-events: none;
        transform: translate(-50%, -50%) scale(0.3);
        animation: atlas-e2e-human-click-ripple 420ms ease-out forwards;
        z-index: 2147483646;
      }
      @keyframes atlas-e2e-human-click-ripple {
        0% {
          opacity: 0.95;
          transform: translate(-50%, -50%) scale(0.3);
        }
        100% {
          opacity: 0;
          transform: translate(-50%, -50%) scale(2.8);
        }
      }
    `;
    document.documentElement.appendChild(style);

    const cursor = document.createElement("div");
    cursor.id = "atlas-e2e-human-cursor";
    cursor.textContent = "^";
    document.documentElement.appendChild(cursor);

    const updateCursorType = (target: EventTarget | null) => {
      const element = target instanceof Element ? target : null;
      const clickable =
        element &&
        (
          element.closest("button, a, input, textarea, select, summary, [role='button'], [data-wf-port='true'], .wf-react-line-add-btn, .wf-react-node-item") ||
          window.getComputedStyle(element).cursor === "pointer"
        );
      cursor.classList.toggle("atlas-e2e-human-cursor-pointer", Boolean(clickable));
      cursor.textContent = clickable ? "☞" : "^";
    };

    const spawnRipple = (clientX: number, clientY: number) => {
      const ripple = document.createElement("div");
      ripple.className = "atlas-e2e-human-click-ripple";
      ripple.style.left = `${clientX}px`;
      ripple.style.top = `${clientY}px`;
      document.documentElement.appendChild(ripple);
      window.setTimeout(() => ripple.remove(), 460);
    };

    window.addEventListener("mousemove", (event) => {
      cursor.style.left = `${event.clientX}px`;
      cursor.style.top = `${event.clientY}px`;
      cursor.classList.add("atlas-e2e-human-cursor-visible");
      updateCursorType(event.target);
    }, { passive: true });

    window.addEventListener("mouseover", (event) => {
      updateCursorType(event.target);
    }, { passive: true });

    window.addEventListener("mousedown", (event) => {
      cursor.style.transform = "translate(-42%, -42%) scale(0.92)";
      spawnRipple(event.clientX, event.clientY);
    });

    window.addEventListener("mouseup", () => {
      cursor.style.transform = "";
    });

    window.addEventListener("mouseleave", () => {
      cursor.classList.remove("atlas-e2e-human-cursor-visible");
    });
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", installHumanCursorOverlay, { once: true });
  } else {
    installHumanCursorOverlay();
  }

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
};

async function clearStorageForOrigin(page: Page, url: string) {
  let lastError: unknown = null;

  for (let attempt = 0; attempt < 3; attempt += 1) {
    await page.goto(url, { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("domcontentloaded");

    try {
      await page.evaluate(() => {
        window.localStorage.clear();
        window.sessionStorage.clear();
      });
      return;
    } catch (error) {
      lastError = error;
      const message = error instanceof Error ? error.message : String(error);
      if (!message.includes("Execution context was destroyed")) {
        throw error;
      }
      await page.waitForTimeout(200);
    }
  }

  if (lastError) {
    throw lastError;
  }
}

async function clearAuthState(page: Page) {
  await page.context().clearCookies();
  await clearStorageForOrigin(page, platformBaseUrl);
  await clearStorageForOrigin(page, appBaseUrl);
  activeAppSessionKey = null;
}

async function isWorkspaceSessionReady(page: Page, appKey: string): Promise<boolean> {
  await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);

  const loginRegex = new RegExp(`/apps/${encodeURIComponent(appKey)}/login(?:\\?.*)?$`);
  if (loginRegex.test(page.url())) {
    return false;
  }

  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
  return true;
}

async function syncAppProfile(page: Page): Promise<boolean> {
  return page.evaluate(
    async ({ profileKey, accessTokenKey, tenantKey }) => {
      const accessToken = sessionStorage.getItem(accessTokenKey) ?? localStorage.getItem(accessTokenKey);
      const tenantId = localStorage.getItem(tenantKey);
      if (!accessToken || !tenantId) {
        return false;
      }

      const response = await fetch("/api/v1/auth/me", {
        method: "GET",
        credentials: "include",
        headers: {
          Authorization: `Bearer ${accessToken}`,
          "X-Tenant-Id": tenantId
        }
      });
      if (!response.ok) {
        return false;
      }

      const payload = (await response.json()) as { data?: unknown };
      if (!payload.data) {
        return false;
      }

      sessionStorage.setItem(profileKey, JSON.stringify(payload.data));
      localStorage.removeItem(profileKey);
      return true;
    },
    {
      profileKey: appProfileStorageKey,
      accessTokenKey: appAccessTokenStorageKey,
      tenantKey: appTenantStorageKey
    }
  );
}

async function ensureFreshWorkspaceSession(page: Page, appKey: string): Promise<void> {
  const synced = await syncAppProfile(page);
  if (!synced) {
    throw new Error("无法同步应用级认证档案，当前会话可能已失效。");
  }

  await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/atlas-space/develop`);
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
}

export const test = base.extend<SessionFixtures>({
  _sharedContext: [
    async ({ browser }, use) => {
      const context = await browser.newContext();
      await context.addInitScript(initResizeObserverSuppression);
      createdContextCount += 1;
      console.log(`[single-session] BrowserContext created: ${createdContextCount}`);
      await use(context);
      await context.close();
    },
    { scope: "worker" }
  ],
  _sharedPage: [
    async ({ _sharedContext }, use) => {
      if (!sharedPage || sharedPage.isClosed()) {
        sharedPage = _sharedContext.pages()[0] ?? (await _sharedContext.newPage());
        sharedPage = createHumanLikePage(sharedPage);
        createdPageCount += 1;
        console.log(`[single-session] Page created: ${createdPageCount}`);
      }

      await use(sharedPage);
    },
    { scope: "worker" }
  ],
  context: async ({ _sharedContext }, use) => {
    await use(_sharedContext);
  },
  page: async ({ _sharedPage }, use) => {
    await use(_sharedPage);
  },
  ensureLoggedInSession: async ({ page }, use) => {
    await use(async (appKey: string) => {
      if (activeAppSessionKey === appKey) {
        const ready = await isWorkspaceSessionReady(page, appKey);
        if (ready) {
          await ensureFreshWorkspaceSession(page, appKey);
          return;
        }
      }

      await clearAuthState(page);
      await loginApp(page, appKey);
      await ensureFreshWorkspaceSession(page, appKey);
      activeAppSessionKey = appKey;
    });
  },
  resetAuthForCase: async ({ page }, use) => {
    await use(async () => {
      await clearAuthState(page);
    });
  }
});

export { expect };
export type { APIRequestContext, BrowserContext, Page, TestInfo } from "@playwright/test";
