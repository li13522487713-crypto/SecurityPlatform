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

export const test = base.extend<SessionFixtures>({
  _sharedContext: [
    async ({ browser }, use) => {
      const context = await browser.newContext();
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
          return;
        }
      }

      await clearAuthState(page);
      await loginApp(page, appKey);
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
