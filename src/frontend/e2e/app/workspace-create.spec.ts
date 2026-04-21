import { expect, test } from "../fixtures/single-session";
import { appBaseUrl, defaultTenantId, ensureAppSetup, loginApp, uniqueName } from "./helpers";
import { orgWorkspacesPath } from "@atlas/app-shell-shared";

test.describe.serial("@smoke Workspace Create", () => {
  let appKey = "";

  test.beforeAll(async ({ request }) => {
    appKey = await ensureAppSetup(request);
  });

  test("create workspace enters the new workspace immediately", async ({ page, resetAuthForCase }) => {
    test.setTimeout(180_000);

    const workspaceName = uniqueName("e2e-workspace");
    const workspaceDescription = `created by ${workspaceName}`;

    await resetAuthForCase();
    await loginApp(page, appKey);
    await expect(page).toHaveURL(new RegExp(`${orgWorkspacesPath(defaultTenantId)}(?:\\?.*)?$`));
    await expect(page.getByTestId("workspace-list-page")).toBeVisible({ timeout: 30_000 });

    await page.getByTestId("workspace-create-btn").click();
    const formModal = page.locator('[role="dialog"]').last();
    await expect(formModal).toBeVisible({ timeout: 30_000 });
    await expect(page.getByPlaceholder(/输入工作空间名称|workspace name/i).last()).toBeVisible({ timeout: 30_000 });
    await page.getByPlaceholder(/输入工作空间名称|workspace name/i).last().fill(workspaceName);
    await page.getByPlaceholder(/输入工作空间描述|workspace description/i).last().fill(workspaceDescription);

    const createResponsePromise = page.waitForResponse((response) => {
      return (
        response.request().method() === "POST" &&
        response.url().includes(`/api/v1/organizations/${encodeURIComponent(defaultTenantId)}/workspaces`)
      );
    }, { timeout: 30_000 });
    await page.locator('[data-testid="workspace-form-submit"]:visible').first().click();

    const createResponse = await createResponsePromise;
    expect(createResponse.ok()).toBeTruthy();
    const createPayload = (await createResponse.json()) as {
      success?: boolean;
      data?: { id?: string | number; Id?: string | number };
    };
    expect(createPayload.success).toBeTruthy();

    const createdWorkspaceId = String(createPayload.data?.id ?? createPayload.data?.Id ?? "").trim();
    expect(createdWorkspaceId).not.toBe("");
    await expect(page).toHaveURL(new RegExp(`/org/${defaultTenantId}/workspaces/${createdWorkspaceId}/dashboard(?:\\?.*)?$`), {
      timeout: 30_000
    });
    await expect(page.getByTestId("workspace-no-app-dashboard")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("workspace-no-app-dashboard")).toContainText(workspaceName);
    await expect(page.getByTestId("workspace-no-app-create")).toBeVisible({ timeout: 30_000 });

    await page.goto(`${appBaseUrl}${orgWorkspacesPath(defaultTenantId)}`);
    const workspaceCard = page.getByTestId(`workspace-card-${createdWorkspaceId}`);
    await expect(workspaceCard).toBeVisible({ timeout: 30_000 });
    await expect(workspaceCard).toContainText(workspaceName);
    await expect(workspaceCard).toContainText(workspaceDescription);
  });
});
