import { expect, test } from "../fixtures/single-session";
import {
  ensureAppSetup,
  navigateBySidebar
} from "./helpers";

test.describe.serial("App Approval Workspace", () => {
  test.fixme("旧壳审批管理页已下线，待新壳对应任务中心能力补齐后恢复。");
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await navigateBySidebar(page, "approval", {
      pageTestId: "app-approval-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/admin/approval(?:\\?.*)?$`)
    });
  });

  test("tabs should switch and table should load", async ({ page }) => {
    await expect(page.getByTestId("app-approval-pending-table")).toBeVisible();
    await page.getByRole("button", { name: /已办|Done/ }).click();
    await expect(page.getByTestId("app-approval-done-table")).toBeVisible();
    await page.getByRole("button", { name: /我发起|My Requests/ }).click();
    await expect(page.getByTestId("app-approval-requests-table")).toBeVisible();
    await page.getByRole("button", { name: /抄送我|CC/ }).click();
    await expect(page.getByTestId("app-approval-cc-table")).toBeVisible();
  });
});

