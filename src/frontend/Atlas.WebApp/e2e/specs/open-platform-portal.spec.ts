import { expect, test } from "@playwright/test";
import { loginAsStoredRole } from "../helpers/test-helpers";

test.describe("open platform portal gui", () => {
  test("can manage project, webhook and view stats/sdk tabs", async ({ page }) => {
    await loginAsStoredRole(page, "aiadmin");

    await page.goto("/ai/open-platform");
    await page.waitForLoadState("domcontentloaded");

    await expect(page.getByText("开放平台开发者门户")).toBeVisible();

    // 1) 开放应用：创建应用并校验 token 结果弹窗
    await page.getByRole("button", { name: "创建开放应用" }).click();
    const createProjectDialog = page.getByRole("dialog", { name: "创建开放应用" });
    const projectInputs = createProjectDialog.locator("input");
    await projectInputs.nth(0).fill(`E2E_Open_${Date.now()}`);
    await createProjectDialog.locator("textarea").fill("E2E GUI 手动流程验证");
    await projectInputs.nth(1).fill("open:*");
    await createProjectDialog.getByRole("button", { name: "确 定" }).click();

    const tokenResultModal = page.getByText("令牌结果").first();
    await expect(tokenResultModal).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText("AppId:")).toBeVisible();
    await expect(page.getByText("AppSecret:")).toBeVisible();
    const closeButtons = page.locator(".ant-modal-close");
    if (await closeButtons.count()) {
      await closeButtons.first().click({ force: true });
    }

    // 2) 调用统计：切换 tab 并查询汇总
    await page.getByRole("tab", { name: "调用统计" }).click();
    await page.getByRole("button", { name: /查\s*询/ }).click();
    await expect(page.getByText("总调用次数")).toBeVisible();
    await expect(page.getByText("成功次数")).toBeVisible();

    // 3) Webhook：创建订阅 + 测试投递 + 打开投递记录
    await page.getByRole("tab", { name: "Webhook" }).click();
    await page.getByRole("button", { name: "创建订阅" }).click();
    const createWebhookDialog = page.getByRole("dialog", { name: "创建订阅" });
    const webhookInputs = createWebhookDialog.locator("input");
    await webhookInputs.nth(0).fill(`E2E_Hook_${Date.now()}`);
    await webhookInputs.nth(1).fill("https://example.com/webhook");
    await createWebhookDialog.locator("input[type='password']").fill("e2e-open-platform-secret");
    await createWebhookDialog.getByRole("button", { name: "确 定" }).click();

    const webhookRowAction = page.getByRole("button", { name: "测试投递" }).first();
    await expect(webhookRowAction).toBeVisible({ timeout: 10_000 });
    await webhookRowAction.click();
    await page.getByRole("button", { name: "投递记录" }).first().click();
    await expect(page.locator(".ant-drawer").first()).toBeVisible();
    await page.locator(".ant-drawer-close").click();

    // 4) SDK 下载：切换 tab 并点击下载按钮（至少校验按钮可见并可交互）
    await page.getByRole("tab", { name: "SDK 下载" }).click();
    const openApiBtn = page.getByRole("button", { name: "下载 OpenAPI 文档" });
    const tsSdkBtn = page.getByRole("button", { name: "下载 TypeScript SDK 包" });
    const csharpSdkBtn = page.getByRole("button", { name: "下载 C# SDK 包" });

    await expect(openApiBtn).toBeVisible();
    await expect(tsSdkBtn).toBeVisible();
    await expect(csharpSdkBtn).toBeVisible();

    await openApiBtn.click();
    await tsSdkBtn.click();
    await csharpSdkBtn.click();
  });
});
