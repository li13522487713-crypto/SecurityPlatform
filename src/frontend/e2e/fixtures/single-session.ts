import {
  expect,
  test as base,
  type BrowserContext,
  type Locator,
  type Page
} from "@playwright/test";
import { selectWorkspacePath, signPath } from "@atlas/app-shell-shared";
import { appBaseUrl, loginApp } from "../app/helpers";
import {
  clamp,
  gazeDelay,
  gazeShiftDelay,
  moveMouseHumanLike,
  pressHoldDuration,
  randomBetween,
  randomClickTarget,
  setMousePosition,
  typingCharDelay
} from "./human-mouse";

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
const decoratedHandles = new WeakSet<object>();
const rawClickSymbol = Symbol("raw-click");
const rawDoubleClickSymbol = Symbol("raw-double-click");
const rawHoverSymbol = Symbol("raw-hover");
const rawFillSymbol = Symbol("raw-fill");
const rawTypeSymbol = Symbol("raw-type");
const rawSelectOptionSymbol = Symbol("raw-selectOption");
const rawCheckSymbol = Symbol("raw-check");
const rawUncheckSymbol = Symbol("raw-uncheck");

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

// ---------------------------------------------------------------------------
// humanClick — move with Bezier path, gaze pause, log-normal press hold
// ---------------------------------------------------------------------------

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

  const destination = randomClickTarget(box, position);
  const targetWidth = Math.min(box.width, box.height);

  await moveMouseHumanLike(page, destination, { targetWidth });

  // Gaze delay — visual confirmation before clicking
  await page.waitForTimeout(clamp(gazeDelay(), 32, 140));

  for (const modifier of modifiers ?? []) {
    await page.keyboard.down(modifier);
  }

  for (let index = 0; index < clickCount; index += 1) {
    await page.mouse.down({ button });
    await page.waitForTimeout(delay > 0 ? delay : clamp(pressHoldDuration(), 28, 180));
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

// ---------------------------------------------------------------------------
// humanHover — Bezier move + gaze pause
// ---------------------------------------------------------------------------

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

  const destination = randomClickTarget(box, position);
  const targetWidth = Math.min(box.width, box.height);

  for (const modifier of modifiers ?? []) {
    await page.keyboard.down(modifier);
  }

  await moveMouseHumanLike(page, destination, { targetWidth });
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

// ---------------------------------------------------------------------------
// humanFill — move to input, click to focus, then type with variable speed
// ---------------------------------------------------------------------------

async function humanFill(
  page: Page,
  locator: Locator,
  value: string,
  options?: { force?: boolean; noWaitAfter?: boolean; timeout?: number }
): Promise<void> {
  const rawFill = (
    (locator as Record<PropertyKey, unknown>)[rawFillSymbol] as
      | ((value: string, options?: Record<string, unknown>) => Promise<void>)
      | undefined
  )?.bind(locator);

  await humanClick(page, locator, { timeout: options?.timeout });
  await page.waitForTimeout(randomBetween(30, 70));

  if (rawFill) {
    await rawFill(value, options);
  } else {
    await locator.fill(value, options);
  }

  if (!options?.noWaitAfter) {
    await page.waitForTimeout(randomBetween(20, 50));
  }
}

// ---------------------------------------------------------------------------
// humanType — move to input, click, then type character-by-character
// ---------------------------------------------------------------------------

async function humanType(
  page: Page,
  locator: Locator,
  text: string,
  options?: { delay?: number; noWaitAfter?: boolean; timeout?: number }
): Promise<void> {
  const rawType = (
    (locator as Record<PropertyKey, unknown>)[rawTypeSymbol] as
      | ((text: string, options?: Record<string, unknown>) => Promise<void>)
      | undefined
  )?.bind(locator);

  await humanClick(page, locator, { timeout: options?.timeout });
  await page.waitForTimeout(randomBetween(30, 60));

  if (options?.delay !== undefined) {
    if (rawType) {
      await rawType(text, options);
    } else {
      await locator.pressSequentially(text, { delay: options.delay });
    }
  } else {
    for (const char of text) {
      await page.keyboard.type(char, { delay: 0 });
      await page.waitForTimeout(typingCharDelay(char));
    }
  }

  if (!options?.noWaitAfter) {
    await page.waitForTimeout(randomBetween(20, 40));
  }
}

// ---------------------------------------------------------------------------
// humanSelectOption — hover to select, then delegate
// ---------------------------------------------------------------------------

async function humanSelectOption(
  page: Page,
  locator: Locator,
  ...args: unknown[]
): Promise<string[]> {
  const rawSelectOption = (
    (locator as Record<PropertyKey, unknown>)[rawSelectOptionSymbol] as
      | ((...a: unknown[]) => Promise<string[]>)
      | undefined
  )?.bind(locator);

  await humanHover(page, locator);
  await page.waitForTimeout(randomBetween(20, 50));

  if (rawSelectOption) {
    return rawSelectOption(...args);
  }
  return locator.selectOption(args[0] as string, args[1] as Record<string, unknown> | undefined);
}

// ---------------------------------------------------------------------------
// humanCheck / humanUncheck — hover then delegate
// ---------------------------------------------------------------------------

async function humanCheck(
  page: Page,
  locator: Locator,
  options?: { force?: boolean; noWaitAfter?: boolean; position?: { x: number; y: number }; timeout?: number; trial?: boolean }
): Promise<void> {
  const rawCheck = (
    (locator as Record<PropertyKey, unknown>)[rawCheckSymbol] as
      | ((options?: Record<string, unknown>) => Promise<void>)
      | undefined
  )?.bind(locator);

  await humanHover(page, locator, { position: options?.position, timeout: options?.timeout });
  await page.waitForTimeout(randomBetween(16, 40));

  if (rawCheck) {
    await rawCheck(options);
  } else {
    await locator.check(options);
  }
}

async function humanUncheck(
  page: Page,
  locator: Locator,
  options?: { force?: boolean; noWaitAfter?: boolean; position?: { x: number; y: number }; timeout?: number; trial?: boolean }
): Promise<void> {
  const rawUncheck = (
    (locator as Record<PropertyKey, unknown>)[rawUncheckSymbol] as
      | ((options?: Record<string, unknown>) => Promise<void>)
      | undefined
  )?.bind(locator);

  await humanHover(page, locator, { position: options?.position, timeout: options?.timeout });
  await page.waitForTimeout(randomBetween(16, 40));

  if (rawUncheck) {
    await rawUncheck(options);
  } else {
    await locator.uncheck(options);
  }
}

// ---------------------------------------------------------------------------
// Handle decoration — intercept Locator methods to inject human behavior
// ---------------------------------------------------------------------------

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
  wrapHandleMethod(target, "fill", rawFillSymbol, (_original, args) =>
    humanFill(page, target as unknown as Locator, args[0] as string, args[1] as Record<string, unknown> | undefined)
  );
  wrapHandleMethod(target, "pressSequentially", rawTypeSymbol, (_original, args) =>
    humanType(page, target as unknown as Locator, args[0] as string, args[1] as Record<string, unknown> | undefined)
  );
  wrapHandleMethod(target, "selectOption", rawSelectOptionSymbol, (_original, args) =>
    humanSelectOption(page, target as unknown as Locator, ...args)
  );
  wrapHandleMethod(target, "check", rawCheckSymbol, (_original, args) =>
    humanCheck(page, target as unknown as Locator, args[0] as Record<string, unknown> | undefined)
  );
  wrapHandleMethod(target, "uncheck", rawUncheckSymbol, (_original, args) =>
    humanUncheck(page, target as unknown as Locator, args[0] as Record<string, unknown> | undefined)
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

// ---------------------------------------------------------------------------
// ResizeObserver suppression + human cursor overlay (injected into page)
// ---------------------------------------------------------------------------

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
        transform: translate(-2px, -1px);
        transition: opacity 120ms ease;
        opacity: 0;
        will-change: transform, opacity;
        filter: drop-shadow(0 1px 3px rgba(15, 23, 42, 0.28));
      }
      #atlas-e2e-human-cursor.atlas-e2e-human-cursor-visible {
        opacity: 1;
      }
      #atlas-e2e-human-cursor.atlas-e2e-human-cursor-pointer svg {
        transform: scale(1.05);
        transform-origin: 4px 1px;
      }
      #atlas-e2e-human-cursor.atlas-e2e-human-cursor-pressing svg {
        transform: scale(0.92);
        transform-origin: 4px 1px;
        transition: transform 60ms cubic-bezier(0.34, 1.56, 0.64, 1);
      }
      .atlas-e2e-human-click-ripple {
        position: fixed;
        width: 18px;
        height: 18px;
        border-radius: 999px;
        pointer-events: none;
        transform: translate(-50%, -50%) scale(0.2);
        animation: atlas-e2e-human-click-ripple 480ms cubic-bezier(0.22, 0.61, 0.36, 1) forwards;
        z-index: 2147483646;
        background: radial-gradient(circle, rgba(59, 130, 246, 0.45) 0%, rgba(59, 130, 246, 0.12) 60%, transparent 100%);
      }
      @keyframes atlas-e2e-human-click-ripple {
        0% {
          opacity: 0.95;
          transform: translate(-50%, -50%) scale(0.2);
        }
        60% {
          opacity: 0.5;
        }
        100% {
          opacity: 0;
          transform: translate(-50%, -50%) scale(3.2);
        }
      }
    `;
    document.documentElement.appendChild(style);

    const cursor = document.createElement("div");
    cursor.id = "atlas-e2e-human-cursor";
    cursor.innerHTML = `<svg width="20" height="24" viewBox="0 0 20 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M1 1L1 18.5L5.5 14.5L9.5 22L12.5 20.5L8.5 12.5L14 12.5L1 1Z"
            fill="white" stroke="#222" stroke-width="1.4" stroke-linejoin="round"/>
    </svg>`;
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
    };

    const spawnRipple = (clientX: number, clientY: number) => {
      const ripple = document.createElement("div");
      ripple.className = "atlas-e2e-human-click-ripple";
      ripple.style.left = `${clientX}px`;
      ripple.style.top = `${clientY}px`;
      document.documentElement.appendChild(ripple);
      window.setTimeout(() => ripple.remove(), 520);
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
      cursor.classList.add("atlas-e2e-human-cursor-pressing");
      spawnRipple(event.clientX, event.clientY);
    });

    window.addEventListener("mouseup", () => {
      cursor.classList.remove("atlas-e2e-human-cursor-pressing");
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

// ---------------------------------------------------------------------------
// Storage & auth helpers
// ---------------------------------------------------------------------------

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
  await clearStorageForOrigin(page, appBaseUrl);
  activeAppSessionKey = null;
}

async function isWorkspaceSessionReady(page: Page, appKey: string): Promise<boolean> {
  await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);

  const loginRegex = new RegExp(`${signPath().replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}(?:\\?.*)?$`);
  if (loginRegex.test(page.url())) {
    return false;
  }

  const workspaceHomePattern = /\/workspace\/[^/]+\/home(?:\?.*)?$/;
  if (workspaceHomePattern.test(new URL(page.url()).pathname + new URL(page.url()).search)) {
    return true;
  }

  await expect(page.getByTestId("coze-select-workspace-page")).toBeVisible({ timeout: 30_000 });
  const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
  if (await matchedWorkspaceButton.count()) {
    await expect(matchedWorkspaceButton).toBeVisible({ timeout: 30_000 });
    return true;
  }

  const anyWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]').first();
  await expect(anyWorkspaceButton).toBeVisible({ timeout: 30_000 });
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

  await page.goto(`${appBaseUrl}${selectWorkspacePath()}`);
  await expect(page.getByTestId("coze-select-workspace-page")).toBeVisible({ timeout: 30_000 });
  const matchedWorkspaceButton = page.locator('[data-testid^="coze-select-workspace-"]', { hasText: appKey }).first();
  if (await matchedWorkspaceButton.count()) {
    await matchedWorkspaceButton.click();
  } else {
    await page.locator('[data-testid^="coze-select-workspace-"]').first().click();
  }
  await page.waitForURL(/\/workspace\/[^/]+\/home(?:\?.*)?$/, { timeout: 30_000 });
  await expect(page.getByTestId("app-sidebar")).toBeVisible({ timeout: 30_000 });
}

// ---------------------------------------------------------------------------
// Fixture definition
// ---------------------------------------------------------------------------

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

export { expect, gazeShiftDelay, setMousePosition };
export type { APIRequestContext, BrowserContext, ConsoleMessage, Page, TestInfo } from "@playwright/test";
