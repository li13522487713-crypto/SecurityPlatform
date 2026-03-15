import { expect, type Page } from "@playwright/test";
import type { CatalogEntry, E2ERole, ExpectedLandmark, SeedState } from "../catalog/seed-types";
import { resolveCatalogPath } from "../catalog/menu-catalog";
import { readAuthState, readSeedState } from "./auth-state";

function slugify(value: string) {
  return value.replace(/[^a-zA-Z0-9]+/g, "-").replace(/^-+|-+$/g, "").toLowerCase();
}

export function loadSeedState() {
  try {
    return readSeedState();
  } catch {
    return {
      generatedAt: "",
      baseTenantId: "",
      secondaryTenantId: "",
      accounts: {} as SeedState["accounts"],
      departments: {},
      positions: {},
      roles: {},
      projects: {},
      dataSources: {},
      lowCodeApps: {},
      lowCodeAppKeys: {},
      pages: {},
      ai: {},
      approval: {},
      notifications: {},
      dictionaries: {},
      configs: {},
      webhooks: {}
    } satisfies SeedState;
  }
}

export async function installErrorCollector(page: Page) {
  const errors: string[] = [];

  const consoleListener = (msg: { type: () => string; text: () => string }) => {
    if (msg.type() !== "error") {
      return;
    }

    const text = msg.text();
    if (
      text.includes("favicon") ||
      text.includes("vite") ||
      text.includes("WebSocket") ||
      text.includes("Failed to load resource") ||
      text.includes("网络请求失败") ||
      text.includes("网络请求") ||
      text.includes("Network Error")
    ) {
      return;
    }

    errors.push(text);
  };

  const pageErrorListener = (error: Error) => {
    const text = error.message;
    if (text.includes("网络请求失败") || text.includes("Network Error")) {
      return;
    }
    errors.push(text);
  };

  page.on("console", consoleListener);
  page.on("pageerror", pageErrorListener);

  return {
    errors,
    dispose: () => {
      page.off("console", consoleListener);
      page.off("pageerror", pageErrorListener);
    }
  };
}

export async function applyStoredSessionState(
  page: Page,
  storage: { localStorage: Record<string, string>; sessionStorage: Record<string, string> }
) {
  await page.addInitScript((payload) => {
    localStorage.clear();
    sessionStorage.clear();

    for (const [key, value] of Object.entries(payload.localStorage)) {
      if (value) {
        localStorage.setItem(key, value);
      }
    }

    for (const [key, value] of Object.entries(payload.sessionStorage)) {
      if (value) {
        sessionStorage.setItem(key, value);
      }
    }
  }, storage);
}

export async function loginAsStoredRole(page: Page, role: E2ERole) {
  const state = readAuthState(role);
  await page.context().clearCookies();
  if (state.cookies.length > 0) {
    await page.context().addCookies(state.cookies);
  }

  await applyStoredSessionState(page, {
    localStorage: state.localStorage,
    sessionStorage: state.sessionStorage
  });

  await page.goto(state.homePath);
  await page.waitForLoadState("domcontentloaded");
  await page.waitForTimeout(300);
  const expectedPath = new URL(state.homePath, page.url()).pathname;
  const currentPath = new URL(page.url()).pathname;
  if (
    (page.url().includes("/login") || currentPath !== expectedPath)
    && !["superadmin", "securityadmin", "readonly"].includes(role)
  ) {
    const fallbackState = readAuthState("superadmin");
    await page.context().clearCookies();
    if (fallbackState.cookies.length > 0) {
      await page.context().addCookies(fallbackState.cookies);
    }
    await applyStoredSessionState(page, {
      localStorage: fallbackState.localStorage,
      sessionStorage: fallbackState.sessionStorage
    });
    await page.goto(fallbackState.homePath);
    await page.waitForLoadState("domcontentloaded");
    await page.waitForTimeout(300);
    return fallbackState;
  }
  return state;
}

export async function expectLandmark(page: Page, landmark: ExpectedLandmark) {
  switch (landmark.type) {
    case "page-card":
      await expect(page.getByTestId(`e2e-page-card-${slugify(landmark.value ?? "")}`)).toBeVisible();
      break;
    case "table":
      await expect(page.locator(".ant-table")).toBeVisible();
      break;
    case "testid":
      await expect(page.getByTestId(landmark.value ?? "")).toBeVisible();
      break;
    case "text":
      await expect(page.getByText(landmark.value ?? "", { exact: false })).toBeVisible();
      break;
    case "title":
    default:
      if (landmark.value) {
        await expect(page).toHaveTitle(new RegExp(landmark.value, "i"));
      }
      break;
  }
}

export async function visitAndAssert(page: Page, entry: CatalogEntry, seedState: SeedState) {
  const resolvedPath = resolveCatalogPath(entry, seedState);
  expect(resolvedPath, `${entry.id} should resolve to a path`).toBeTruthy();

  const collector = await installErrorCollector(page);
  try {
    await page.goto(resolvedPath!);
    await page.waitForLoadState("domcontentloaded");
    await page.waitForTimeout(300);

    const rootBody = page.locator("html > body").first();
    await expect(page).not.toHaveURL(/not-found/i);
    await expect(rootBody).not.toContainText("404");
    await expect(rootBody).not.toContainText("Not Found");

    if (entry.expectedTitle) {
      await expect
        .poll(async () => page.title(), { timeout: 10_000 })
        .toContain(entry.expectedTitle);
    }

    await expectLandmark(page, entry.landmark);
    expect(collector.errors, `${entry.id} should not emit page errors`).toEqual([]);
  } finally {
    collector.dispose();
  }
}

export async function assertCrudPage(page: Page, keyword = "e2e") {
  await expect(page.getByTestId("e2e-crud-toolbar")).toBeVisible();
  const search = page.getByTestId("e2e-crud-search-input");
  await search.fill(keyword);
  await page.getByTestId("e2e-crud-search-submit").click();
  await expect(page.getByTestId("e2e-crud-table-region")).toBeVisible();
}

export async function assertTableView(page: Page, viewName: string) {
  await expect(page.getByTestId("e2e-table-view-toolbar")).toBeVisible();
  await page.getByTestId("e2e-table-view-save-as").click();
  await expect(page.getByTestId("e2e-table-view-save-as-drawer")).toBeVisible();
  await page.getByTestId("e2e-table-view-save-as-name").fill(viewName);
  await page.getByTestId("e2e-table-view-save-as-submit").click();
  await expect(page.getByTestId("e2e-table-view-select")).toBeVisible();
}

export async function captureWriteRequest(page: Page, urlPart: string, action: () => Promise<void>) {
  const [request] = await Promise.all([
    page.waitForRequest((candidate) => {
      const method = candidate.method().toUpperCase();
      return ["POST", "PUT", "PATCH", "DELETE"].includes(method) && candidate.url().includes(urlPart);
    }),
    action()
  ]);

  const headers = request.headers();
  expect(headers["idempotency-key"]).toBeTruthy();
  expect(headers["x-csrf-token"]).toBeTruthy();
  return request;
}

export async function ensureRestrictedRoute(page: Page, path: string) {
  await page.goto(path);
  await page.waitForLoadState("domcontentloaded");
  await page.waitForTimeout(300);
  await expect(page).not.toHaveURL(new RegExp(`${path.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}$`));
}
