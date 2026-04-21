import { expect, test } from "../fixtures/single-session";
import {
  clickCrudSubmit,
  ensureAppSetup,
  navigateBySidebar,
  uniqueName,
  waitForCrudDrawerClosed
} from "./helpers";

test.describe.serial("App Model Configs CRUD", () => {
  test.fixme("旧壳 Model Configs CRUD 页面已随 /workspace/* 收敛下线，待新壳场景重建后恢复。");
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test.beforeEach(async ({ page }) => {
    await navigateBySidebar(page, "model-configs", {
      pageTestId: "app-model-configs-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/model-configs(?:\\?.*)?$`)
    });
    await expect(page.getByTestId("app-model-configs-create")).toBeVisible({ timeout: 30_000 });
  });

  test("create and delete model config should work", async ({ page }) => {
    const modelName = uniqueName("E2EModel");

    await page.getByTestId("app-model-configs-create").click();
    await page.getByTestId("app-model-config-name").fill(modelName);
    await page.getByTestId("app-model-config-api-key").fill("sk-e2e-demo-key");
    await page.getByTestId("app-model-config-base-url").fill("https://api.openai.com/v1");
    await page.getByTestId("app-model-config-default-model").fill("gpt-4o-mini");
    await clickCrudSubmit(page);
    await waitForCrudDrawerClosed(page, "app-model-config-name");

    await expect(page.getByTestId("app-model-configs-grid")).toContainText(modelName);

    const createdCard = page.getByTestId("app-model-configs-grid").locator("article", { hasText: modelName }).first();
    await expect(createdCard).toBeVisible();
    await createdCard.getByText("删除").click();
    await clickCrudSubmit(page);
    await expect(page.getByTestId("app-model-configs-grid")).not.toContainText(modelName);
  });
});
