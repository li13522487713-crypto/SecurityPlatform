import { expect, test } from "../fixtures/single-session";
import {
  appBaseUrl,
  clickCrudSubmit,
  ensureAppSetup,
  navigateBySidebar,
  uniqueName,
  waitForCrudDrawerClosed
} from "./helpers";

const deepseekApiKey = process.env.PLAYWRIGHT_DEEPSEEK_API_KEY ?? "";
const deepseekBaseUrl = process.env.PLAYWRIGHT_DEEPSEEK_BASE_URL ?? "https://api.deepseek.com/v1";
const deepseekModel = process.env.PLAYWRIGHT_DEEPSEEK_MODEL ?? "deepseek-chat";

function buildSecurityIncidentWorkflowCanvas(): string {
  return JSON.stringify(
    {
      nodes: [
        {
          key: "entry_1",
          type: "Entry",
          title: "开始",
          layout: { x: 120, y: 120, width: 160, height: 60 },
          configs: {},
          inputMappings: {}
        },
        {
          key: "text_1",
          type: "TextProcessor",
          title: "安全事件结构化",
          layout: { x: 380, y: 120, width: 220, height: 80 },
          configs: {
            template: JSON.stringify({
              title: "安全事件处置任务",
              summary: "{{incident}}",
              severity: "高",
              ownerSuggestion: "SecurityAdmin",
              nextActions: [
                "确认告警主机与受影响账号",
                "隔离主机并保留取证材料",
                "启动横向移动排查与口令轮换"
              ]
            }),
            outputKey: "result"
          },
          inputMappings: {}
        },
        {
          key: "exit_1",
          type: "Exit",
          title: "结束",
          layout: { x: 700, y: 120, width: 160, height: 60 },
          configs: {},
          inputMappings: {}
        }
      ],
      connections: [
        {
          fromNode: "entry_1",
          fromPort: "output",
          toNode: "text_1",
          toPort: "input",
          condition: null
        },
        {
          fromNode: "text_1",
          fromPort: "output",
          toNode: "exit_1",
          toPort: "input",
          condition: null
        }
      ]
    },
    null,
    2
  );
}

async function chooseSemiOption(page: import("@playwright/test").Page, trigger: import("@playwright/test").Locator, optionText: string) {
  const selection = trigger.locator(".semi-select-selection").first();
  if (await selection.count()) {
    await selection.click();
  } else {
    await trigger.click();
  }

  const option = page.locator(".semi-select-option:visible").filter({ hasText: optionText }).first();
  await expect(option).toBeVisible({ timeout: 30_000 });
  await option.click();
}

test.describe.serial("App Agent Workbench DeepSeek + Workflow", () => {
  test.skip(!deepseekApiKey, "需要通过 PLAYWRIGHT_DEEPSEEK_API_KEY 提供真实 DeepSeek Key。");

  let appKey = "";
  let modelConfigName = "";
  let workflowName = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  test("should create model, chat with bot, and finish workflow task", async ({ page }) => {
    test.setTimeout(240_000);

    modelConfigName = uniqueName("DeepSeekModel");
    workflowName = uniqueName("SecurityIncidentFlow");

    await navigateBySidebar(page, "model-configs", {
      pageTestId: "app-model-configs-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/model-configs(?:\\?.*)?$`)
    });

    await page.getByTestId("app-model-configs-create").click();
    await page.getByTestId("app-model-config-name").fill(modelConfigName);
    await chooseSemiOption(
      page,
      page.locator(".module-studio__field").filter({ hasText: "供应商" }).locator(".semi-select").first(),
      "DeepSeek"
    );
    await page.getByTestId("app-model-config-api-key").fill(deepseekApiKey);
    await page.getByTestId("app-model-config-base-url").fill(deepseekBaseUrl);
    await page.getByTestId("app-model-config-default-model").fill(deepseekModel);
    await clickCrudSubmit(page);
    await waitForCrudDrawerClosed(page, "app-model-config-name");

    const modelCard = page.getByTestId("app-model-configs-grid").locator("article", { hasText: modelConfigName }).first();
    await expect(modelCard).toBeVisible({ timeout: 30_000 });
    await modelCard.getByText("编辑").click();

    await page.getByTestId("app-model-config-test-connection").click();
    await expect(page.getByTestId("app-model-config-test-result")).toContainText("连通成功", { timeout: 90_000 });
    await page.getByTestId("app-model-config-test-prompt").click();
    await expect(page.getByTestId("app-model-config-test-result")).not.toContainText("正在执行 Prompt 测试...", { timeout: 120_000 });
    await expect(page.getByTestId("app-model-config-test-result")).not.toBeEmpty({ timeout: 120_000 });
    await clickCrudSubmit(page);
    await waitForCrudDrawerClosed(page, "app-model-config-name");

    await navigateBySidebar(page, "develop", {
      pageTestId: "app-develop-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/develop(?:\\?.*)?$`)
    });

    await page.getByTestId("app-develop-create-agent").click();
    await expect(page.getByTestId("app-bot-ide-page")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("app-bot-ide-resource-status")).toContainText("模型", { timeout: 30_000 });

    const botUrl = new URL(page.url());
    const match = botUrl.pathname.match(/\/apps\/([^/]+)\/space\/([^/]+)\/bot\/([^/]+)/);
    expect(match).toBeTruthy();
    const [, , spaceId, botId] = match as RegExpMatchArray;

    await chooseSemiOption(page, page.getByTestId("app-bot-ide-model-config"), modelConfigName);
    await page.getByTestId("app-bot-ide-save").click();

    await navigateBySidebar(page, "workflows", {
      pageTestId: "app-workflows-page",
      urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/work_flow(?:\\?.*)?$`)
    });

    await page.getByTestId("app-workflows-create").click();
    await page.getByPlaceholder("输入Workflow名称").fill(workflowName);
    await clickCrudSubmit(page);

    await expect(page.getByTestId("app-workflow-editor-page")).toBeVisible({ timeout: 30_000 });
    await page.getByTestId("workflow.detail.canvas-json").fill(buildSecurityIncidentWorkflowCanvas());
    await page.getByTestId("workflow.detail.title.save-draft").click();
    await page.getByTestId("workflow-base-publish-button").click();
    await expect(page.getByTestId("workflow.detail.title.save-draft")).toBeVisible({ timeout: 30_000 });

    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/space/${encodeURIComponent(spaceId)}/bot/${encodeURIComponent(botId)}`);
    await expect(page.getByTestId("app-bot-ide-page")).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId("app-bot-ide-resource-status")).toContainText("工作流", { timeout: 30_000 });

    await chooseSemiOption(page, page.getByTestId("app-bot-ide-workflow-select"), workflowName);
    await page.getByTestId("app-bot-ide-bind-workflow").click();
    await page.getByTestId("app-bot-ide-save").click();

    await page.getByTestId("app-bot-ide-message-input").fill("请用一句话确认你已经接入安全事件处置工作台。");
    await page.getByTestId("app-bot-ide-send").click();
    await expect(page.getByTestId("app-bot-ide-messages")).toContainText("assistant", { timeout: 120_000 });

    await page.getByTestId("app-bot-ide-workflow-input").fill("主机检测到可疑 PowerShell 横向移动行为，需要立即安排隔离、排查和取证。");
    await page.getByTestId("app-bot-ide-run-workflow").click();

    await expect(page.getByTestId("app-bot-ide-messages")).toContainText("严重级别", { timeout: 60_000 });
    await expect(page.getByTestId("app-bot-ide-messages")).toContainText("SecurityAdmin", { timeout: 60_000 });
    await expect(page.getByTestId("app-bot-ide-trace")).toContainText("Execution", { timeout: 60_000 });
  });
});
