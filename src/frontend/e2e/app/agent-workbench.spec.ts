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

async function openDevelopCreateAction(page: import("@playwright/test").Page, actionText: string) {
  await page.getByTestId("app-develop-create-menu").click();
  const action = page.locator(".module-studio__coze-menu-item").filter({ hasText: actionText }).last();
  await expect(action).toBeVisible({ timeout: 30_000 });
  await action.click();
}

async function createAgentFromDevelop(
  page: import("@playwright/test").Page,
  appKey: string,
  agentName: string,
  description = "用于 E2E 校验最新 Agent Workbench 结构。"
) {
  await navigateBySidebar(page, "develop", {
    pageTestId: "app-develop-page",
    urlPattern: new RegExp(`/apps/${encodeURIComponent(appKey)}/space/[^/]+/develop(?:\\?.*)?$`)
  });

  await openDevelopCreateAction(page, "新建智能体");
  await page.getByPlaceholder("智能体名称").fill(agentName);
  await page.getByPlaceholder("角色描述").fill(description);
  await clickCrudSubmit(page);
  await expect(page.getByTestId("app-bot-ide-page")).toBeVisible({ timeout: 30_000 });
}

function getCurrentAgentId(page: import("@playwright/test").Page): string {
  const url = new URL(page.url());
  const match =
    url.pathname.match(/\/bot\/([^/]+)/) ??
    url.pathname.match(/\/studio\/assistants\/([^/]+)/);
  expect(match).toBeTruthy();
  return decodeURIComponent((match as RegExpMatchArray)[1]);
}

test.describe.serial("App Agent Workbench DeepSeek + Workflow", () => {
  test.fixme("旧壳 Agent Workbench 页面路径已下线，待新壳编辑器场景补齐后恢复。");
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

    await openDevelopCreateAction(page, "新建智能体");
    await page.getByPlaceholder("智能体名称").fill(uniqueName("DeepSeekBot"));
    await page.getByPlaceholder("角色描述").fill("用于联调 DeepSeek 与工作流的智能体。");
    await clickCrudSubmit(page);
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

test.describe.serial("Agent Workbench New Components", () => {
  let appKey = "";

  test.beforeAll(async ({ request, ensureLoggedInSession }) => {
    appKey = await ensureAppSetup(request);
    await ensureLoggedInSession(appKey);
  });

  // create agent dialog → onOpenBot navigate 链路当前在轻量 mock 下不触发 bot 详情路由，
  // app-bot-ide-page 持续不可见。详见 docs/e2e-baseline-failures.md §3 #34。
  test.fixme("should cover segmented nav, synced config panel, debug area, version history, and publish modal", async ({ page }) => {
    const agentName = uniqueName("WorkbenchNav");
    await createAgentFromDevelop(page, appKey, agentName);

    const agentId = getCurrentAgentId(page);
    const publishRoute = new RegExp(`/api/v1/ai-assistants/${encodeURIComponent(agentId)}/publish(?:\\?.*)?$`);
    const publishHandler = async (route: import("@playwright/test").Route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          success: true,
          code: "SUCCESS",
          message: "OK",
          traceId: "agent-workbench-e2e",
          data: {
            publicationId: "pub-agent-workbench-e2e",
            agentId,
            version: 1,
            embedToken: "embed-token-e2e-1234567890",
            embedTokenExpiresAt: new Date("2026-12-31T00:00:00.000Z").toISOString()
          }
        })
      });
    };

    await page.route(publishRoute, publishHandler);

    try {
      const nav = page.getByRole("navigation", { name: "Agent configuration" });
      await expect(nav).toBeVisible();
      await expect(nav.getByRole("tab", { name: "基础" })).toBeVisible();
      await expect(nav.getByRole("tab", { name: "模型" })).toBeVisible();
      await expect(nav.getByRole("tab", { name: "知识库" })).toBeVisible();
      await expect(nav.getByRole("tab", { name: "变量" })).toBeVisible();

      await nav.getByRole("tab", { name: "知识库" }).click();
      await expect(page.getByText("知识库", { exact: true }).last()).toBeVisible();
      await expect(page.getByText("选择知识库并配置检索参数。")).toBeVisible();

      await nav.getByRole("tab", { name: "变量" }).click();
      await expect(page.getByText("暴露给工具与编排的 Bot 变量。")).toBeVisible();
      await expect(page.locator("[data-active-nav='variables']")).toBeVisible();

      await expect(page.getByText("预览与调试")).toBeVisible();
      await expect(page.getByTestId("app-bot-ide-messages")).toBeVisible();
      await expect(page.getByText("当前还没有发布记录。")).toBeVisible();

      const debugCard = page.locator(".module-studio__agent-debug-card").filter({ hasText: "发布与嵌入" }).first();
      const publishResponsePromise = page.waitForResponse((response) => publishRoute.test(response.url()) && response.request().method() === "POST");
      await debugCard.getByRole("button", { name: "发布" }).click();

      await expect(page.getByRole("dialog")).toContainText("发布智能体");
      await page.locator(".semi-modal-content textarea").fill("E2E 校验最新三栏工作台结构与发布弹窗。");
      await page.getByRole("button", { name: "确认发布" }).click();
      await publishResponsePromise;

      await expect(page.getByRole("dialog")).toBeHidden({ timeout: 30_000 });
      await expect(page.getByText("v1")).toBeVisible();
      await expect(page.getByText("当前激活")).toBeVisible();
      await expect(page.getByText("embed-token-e2")).toBeVisible();
    } finally {
      await page.unroute(publishRoute, publishHandler);
    }
  });
});
