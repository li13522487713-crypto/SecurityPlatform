import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, ensureAppSetup, navigateBySidebar, uniqueName } from "./helpers";

test.describe.serial("@microflow Mendix studio create/save", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("create microflow from studio and deep-link open", async ({ page }) => {
    await navigateBySidebar(page, "projects", { urlPattern: /\/workspace\/[^/]+\/projects(?:\?.*)?$/ });
    const workspaceId = page.url().match(/\/workspace\/([^/]+)/)?.[1];
    expect(workspaceId).toBeTruthy();

    const appCard = page.locator('[data-testid="workspace-project-card"]').first();
    if (await appCard.count()) {
      await appCard.click();
    } else {
      await page.goto(`${appBaseUrl}/space/${encodeURIComponent(String(workspaceId))}/mendix-studio/${encodeURIComponent(appKey)}`);
    }

    await page.waitForURL(/\/space\/[^/]+\/mendix-studio\/[^/?]+(?:\?.*)?$/);
    await expect(page.locator(".mendix-studio-root")).toBeVisible({ timeout: 30_000 });

    const name = uniqueName("E2E_MF_CREATE").replace(/-/g, "_");
    await page.request.post("http://127.0.0.1:5002/api/v1/microflows", {
      headers: { "Content-Type": "application/json", "X-Tenant-Id": "00000000-0000-0000-0000-000000000001" },
      data: {
        workspaceId,
        input: {
          name,
          displayName: name,
          moduleId: "Sales",
          moduleName: "Sales",
          tags: ["e2e"],
          parameters: [],
          returnType: { kind: "void" },
          template: "blank"
        }
      }
    });

    const listResp = await page.request.get(`http://127.0.0.1:5002/api/v1/microflows?workspaceId=${encodeURIComponent(String(workspaceId))}`, {
      headers: { "X-Tenant-Id": "00000000-0000-0000-0000-000000000001" }
    });
    expect(listResp.ok()).toBeTruthy();
    const listJson = await listResp.json();
    const created = (listJson?.data?.items ?? []).find((item: { name?: string; id?: string }) => item.name === name);
    expect(created?.id).toBeTruthy();

    await page.goto(`${appBaseUrl}/space/${encodeURIComponent(String(workspaceId))}/mendix-studio/${encodeURIComponent(appKey)}?microflowId=${encodeURIComponent(String(created.id))}`);
    await page.waitForURL(/microflowId=/);
    await expect(page.locator(".mendix-studio-root")).toBeVisible();
  });
});
