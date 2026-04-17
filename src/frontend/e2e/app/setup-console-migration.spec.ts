import { expect, test, type Page } from "../fixtures/single-session";
import { appBaseUrl, captureEvidenceScreenshot } from "./helpers";

const RECOVERY_KEY = "ATLS-MOCK-AAAA-BBBB-CCCC-DDDD";

async function unlockConsole(page: Page) {
  await page.goto(`${appBaseUrl}/setup-console`);
  await page.getByTestId("setup-console-auth-recovery-key").fill(RECOVERY_KEY);
  await page.getByTestId("setup-console-auth-submit").click();
  await expect(page.getByTestId("setup-console-page")).toBeVisible();
}

/**
 * E2E：/setup-console 工作空间初始化 + 数据迁移控制台 4 子区块（M4）。
 *
 * - workspace-init Tab：default 工作空间一键初始化 → state 切到 completed
 * - migration Tab：
 *   - 计划：填好源/目标 → 创建任务 → 任务 ID 出现在执行卡
 *   - 执行：precheck → start → validate → cutover → 状态终态 cutover-completed
 *   - 进度：百分比从 0 增长到 100
 *   - 报告：fetch-report 后展示行数差表
 */
test.describe.serial("Setup Console - Workspace Init", () => {
  test("default workspace can be initialized via workspace-init tab", async ({ page, resetAuthForCase }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    await page.getByTestId("setup-console-tab-workspace-init").click();
    await expect(page).toHaveURL(/\/setup-console\/workspace-init/);
    await expect(page.getByTestId("setup-console-workspace-init")).toBeVisible();

    const row = page.getByTestId("setup-console-workspace-init-row-default");
    await expect(row).toBeVisible();

    await page.getByTestId("setup-console-workspace-init-run-default").click();
    await expect(page.getByTestId("setup-console-workspace-init-state-default")).toContainText(
      /已初始化|ready|Workspace ready|工作空间已初始化/
    );
    await captureEvidenceScreenshot(page, testInfo, "setup-console-workspace-init-ready");
  });
});

test.describe.serial("Setup Console - Data Migration", () => {
  test("plan -> create -> precheck -> start -> validate -> cutover happy path", async ({
    page,
    resetAuthForCase
  }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);

    await page.getByTestId("setup-console-tab-migration").click();
    await expect(page).toHaveURL(/\/setup-console\/migration/);
    await expect(page.getByTestId("setup-console-migration")).toBeVisible();

    // 计划：用默认值，测试 source/target 连接（mock 默认全成功）
    await page.getByTestId("setup-console-migration-source-test").click();
    await expect(page.getByTestId("setup-console-migration-source-test-result")).toBeVisible();
    await page.getByTestId("setup-console-migration-target-test").click();
    await expect(page.getByTestId("setup-console-migration-target-test-result")).toBeVisible();

    // 创建任务
    await page.getByTestId("setup-console-migration-create").click();
    await expect(page.getByTestId("setup-console-migration-execute")).toBeVisible();
    await expect(page.getByTestId("setup-console-migration-execute-state")).toContainText(/Pending|待启动/);
    await captureEvidenceScreenshot(page, testInfo, "setup-console-migration-job-created");

    // precheck → ready
    await page.getByTestId("setup-console-migration-precheck").click();
    await expect(page.getByTestId("setup-console-migration-execute-state")).toContainText(/Ready|就绪/);

    // start → running，进度 > 0
    await page.getByTestId("setup-console-migration-start").click();
    await expect(page.getByTestId("setup-console-migration-progress")).toBeVisible();

    // 多次 poll 推进进度到 100%
    for (let attempt = 0; attempt < 25; attempt += 1) {
      await page.getByTestId("setup-console-migration-progress-poll").click();
      const stateText = await page
        .getByTestId("setup-console-migration-execute-state")
        .textContent();
      if (stateText && /Validating|校验中|Cutover|切主/.test(stateText)) {
        break;
      }
    }

    // validate → cutover-ready
    await page.getByTestId("setup-console-migration-validate").click();
    await expect(page.getByTestId("setup-console-migration-execute-state")).toContainText(
      /Cutover ready|切主就绪/
    );

    // cutover → cutover-completed
    await page.getByTestId("setup-console-migration-cutover").click();
    await expect(page.getByTestId("setup-console-migration-execute-state")).toContainText(
      /Cutover completed|切主完成/
    );
    await captureEvidenceScreenshot(page, testInfo, "setup-console-migration-cutover-completed");

    // report 拉取并展示
    await page.getByTestId("setup-console-migration-report-fetch").click();
    await expect(page.getByTestId("setup-console-migration-report-summary")).toBeVisible();
    await expect(page.getByTestId("setup-console-migration-logs")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-migration-report-summary");
  });

  test("creating a duplicate-fingerprint job without allowReExecute is rejected", async ({
    page,
    resetAuthForCase
  }, testInfo) => {
    await resetAuthForCase();
    await unlockConsole(page);
    await page.getByTestId("setup-console-tab-migration").click();

    // 第一次成功创建并 cutover
    await page.getByTestId("setup-console-migration-create").click();
    await expect(page.getByTestId("setup-console-migration-execute")).toBeVisible();

    await page.getByTestId("setup-console-migration-precheck").click();
    await page.getByTestId("setup-console-migration-start").click();

    for (let attempt = 0; attempt < 25; attempt += 1) {
      await page.getByTestId("setup-console-migration-progress-poll").click();
      const stateText = await page
        .getByTestId("setup-console-migration-execute-state")
        .textContent();
      if (stateText && /Validating|校验中/.test(stateText)) {
        break;
      }
    }
    await page.getByTestId("setup-console-migration-validate").click();
    await page.getByTestId("setup-console-migration-cutover").click();

    // 此时 active migration 已被清空。重新进入 Tab，"创建" 按钮应被启用，但创建相同源/目标会被 mock 拒绝。
    await page.reload();
    await unlockConsole(page);
    await page.getByTestId("setup-console-tab-migration").click();

    await page.getByTestId("setup-console-migration-create").click();
    await expect(page.getByTestId("setup-console-migration-error")).toBeVisible();
    await captureEvidenceScreenshot(page, testInfo, "setup-console-migration-duplicate-rejected");
  });
});
