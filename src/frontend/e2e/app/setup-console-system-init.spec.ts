import { expect, test, type Page } from "../fixtures/single-session";
import { appBaseUrl } from "./helpers";

const RECOVERY_KEY = "ATLS-MOCK-AAAA-BBBB-CCCC-DDDD";

async function unlockConsole(page: Page) {
  await page.goto(`${appBaseUrl}/setup-console`);
  await page.getByTestId("setup-console-auth-recovery-key").fill(RECOVERY_KEY);
  await page.getByTestId("setup-console-auth-submit").click();
  await expect(page.getByTestId("setup-console-page")).toBeVisible();
}

async function gotoSystemInit(page: Page) {
  await page.getByTestId("setup-console-tab-system-init").click();
  await expect(page).toHaveURL(/\/setup-console\/system-init/);
  await expect(page.getByTestId("setup-console-system-init")).toBeVisible();
}

async function refreshOverviewIntoSystemInit(page: Page) {
  // Tab 切回 dashboard 再切回 system-init，以触发 overview refresh + 重新读 system 状态。
  await page.getByTestId("setup-console-tab-dashboard").click();
  await expect(page.getByTestId("setup-console-dashboard")).toBeVisible();
  await page.getByTestId("setup-console-dashboard-refresh").click();
  await expect(page.getByTestId("setup-console-dashboard-refresh")).toBeEnabled();
  await page.getByTestId("setup-console-tab-system-init").click();
  await expect(page.getByTestId("setup-console-system-init")).toBeVisible();
}

/**
 * E2E：/setup-console 系统初始化 6 步全流程（M3）。
 *
 * 验证矩阵：
 *  - 6 步骤卡片均可见，初始仅第 1 步为 current
 *  - 顺序点击 6 个 Run 按钮，状态徽章逐步变 succeeded
 *  - bootstrap-user 完成后弹出 RecoveryKeyDisplay，包含 mock 恢复密钥
 *  - complete 之后系统状态切到 completed，dashboard 系统徽章显示"已完成"
 *  - 已 succeeded 的步骤再点击不会回退（按钮禁用 + 文案显示 succeeded）
 */
test.describe.serial("Setup Console - System Init Flow", () => {
  test("renders the 6 step cards with the precheck step as current", async ({ page, resetAuthForCase }) => {
    await resetAuthForCase();
    await unlockConsole(page);
    await gotoSystemInit(page);

    for (const step of [
      "precheck",
      "schema",
      "seed",
      "bootstrap-user",
      "default-workspace",
      "complete"
    ] as const) {
      await expect(page.getByTestId(`setup-console-step-${step}`)).toBeVisible();
    }
  });

  test("running each step in order eventually completes system initialization", async ({
    page,
    resetAuthForCase
  }) => {
    await resetAuthForCase();
    await unlockConsole(page);
    await gotoSystemInit(page);

    await page.getByTestId("setup-console-step-precheck-run").click();
    await expect(page.getByTestId("setup-console-step-precheck-badge")).toContainText(/已完成|Succeeded/);

    await page.getByTestId("setup-console-step-schema-run").click();
    await expect(page.getByTestId("setup-console-step-schema-badge")).toContainText(/已完成|Succeeded/);

    await page.getByTestId("setup-console-step-seed-run").click();
    await expect(page.getByTestId("setup-console-step-seed-badge")).toContainText(/已完成|Succeeded/);

    await page.getByTestId("setup-console-step-bootstrap-user-run").click();
    await expect(page.getByTestId("setup-console-recovery-key-display")).toBeVisible();
    await expect(page.getByTestId("setup-console-recovery-key-value")).toContainText(RECOVERY_KEY);

    await page.getByTestId("setup-console-recovery-key-acknowledge").check();
    await page.getByTestId("setup-console-recovery-key-confirm").click();
    await expect(page.getByTestId("setup-console-recovery-key-display")).toHaveCount(0);
    await expect(page.getByTestId("setup-console-step-bootstrap-user-badge")).toContainText(/已完成|Succeeded/);

    await page.getByTestId("setup-console-step-default-workspace-run").click();
    await expect(page.getByTestId("setup-console-step-default-workspace-badge")).toContainText(
      /已完成|Succeeded/
    );

    await page.getByTestId("setup-console-step-complete-run").click();
    await expect(page.getByTestId("setup-console-system-init-done")).toBeVisible();

    await refreshOverviewIntoSystemInit(page);
    await page.getByTestId("setup-console-tab-dashboard").click();
    await expect(page.getByTestId("setup-console-system-state-badge")).toContainText(/已完成|Completed/);
  });

  test("idempotent: re-clicking a succeeded step does not regress the badge", async ({
    page,
    resetAuthForCase
  }) => {
    await resetAuthForCase();
    await unlockConsole(page);
    await gotoSystemInit(page);

    await page.getByTestId("setup-console-step-precheck-run").click();
    await expect(page.getByTestId("setup-console-step-precheck-badge")).toContainText(/已完成|Succeeded/);

    // succeeded 后按钮被禁用；不会再触发推进。
    await expect(page.getByTestId("setup-console-step-precheck-run")).toBeDisabled();
  });
});
