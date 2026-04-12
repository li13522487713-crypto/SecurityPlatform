import {
  expect,
  test as base,
  type BrowserContext,
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

async function isDashboardSessionReady(page: Page, appKey: string): Promise<boolean> {
  await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);

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

async function ensureFreshDashboardSession(page: Page, appKey: string): Promise<void> {
  const synced = await syncAppProfile(page);
  if (!synced) {
    throw new Error("无法同步应用级认证档案，当前会话可能已失效。");
  }

  await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/dashboard`);
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
        const ready = await isDashboardSessionReady(page, appKey);
        if (ready) {
          await ensureFreshDashboardSession(page, appKey);
          return;
        }
      }

      await clearAuthState(page);
      await loginApp(page, appKey);
      await ensureFreshDashboardSession(page, appKey);
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
