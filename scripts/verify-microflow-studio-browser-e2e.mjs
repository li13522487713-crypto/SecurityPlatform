#!/usr/bin/env node
import { mkdirSync, writeFileSync } from "node:fs";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendRequire = createRequire(resolve(__dirname, "../src/frontend/package.json"));
const { chromium } = frontendRequire("@playwright/test");

const studioUrl = process.env.STUDIO_URL ?? "http://localhost:5181/space/1496769226340306944/mendix-studio/1496887102913122304";
const apiBaseUrl = (process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002/api/v1").replace(/\/+$/u, "");
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
const username = process.env.MICROFLOW_USERNAME ?? "admin";
const password = process.env.MICROFLOW_PASSWORD ?? "P@ssw0rd!";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "1496769226340306944";
const prefix = process.env.MICROFLOW_E2E_PREFIX ?? "R61_BROWSER_";
const runName = `${prefix}${new Date().toISOString().replace(/[-:TZ.]/gu, "").slice(0, 14)}`;
const artifactsDir = resolve(process.cwd(), "artifacts/microflow-studio-browser-e2e");
const browserExecutablePath = process.env.PLAYWRIGHT_BROWSER_EXECUTABLE_PATH
  ?? [
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
    "C:/Program Files (x86)/Google/Chrome/Application/chrome.exe",
    "C:/Program Files/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"
  ].find(path => existsSync(path));

function apiUrl(path) {
  return `${apiBaseUrl}${path.startsWith("/") ? path : `/${path}`}`;
}

async function loginForBrowserContext(context, request) {
  const response = await request.post(apiUrl("/auth/token"), {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    data: { username, password }
  });
  if (!response.ok()) {
    throw new Error(`登录失败：HTTP ${response.status()} ${await response.text()}`);
  }
  const payload = await response.json();
  const data = payload.data ?? payload;
  if (!data.accessToken) {
    throw new Error("登录响应缺少 accessToken。");
  }

  await context.addInitScript(({ accessToken, refreshToken, tenantId, workspaceId }) => {
    window.sessionStorage.setItem("atlas_app_access_token", accessToken);
    window.localStorage.setItem("atlas_app_refresh_token", refreshToken ?? "");
    window.localStorage.setItem("atlas_app_tenant_id", tenantId);
    window.localStorage.setItem("atlas_last_workspace_id", workspaceId);
    window.localStorage.setItem("atlas_locale", "zh-CN");
  }, {
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    tenantId,
    workspaceId
  });
}

async function screenshot(page, name) {
  mkdirSync(artifactsDir, { recursive: true });
  const file = resolve(artifactsDir, `${name}.png`);
  await page.screenshot({ path: file, fullPage: true });
  return file;
}

async function assertVisible(locator, label) {
  await locator.waitFor({ state: "visible", timeout: 30_000 });
  console.log(`PASS ${label}`);
}

async function findPanelItem(page, target) {
  return page.locator("[data-testid^='microflow-node-panel-item-']").evaluateAll((nodes, expected) => nodes.some(node => {
    const testId = node.getAttribute("data-testid") ?? "";
    const registryKey = node.getAttribute("data-registry-key") ?? "";
    const nodeType = node.getAttribute("data-node-type") ?? "";
    const actionKind = node.getAttribute("data-action-kind") ?? "";
    const text = node.textContent?.trim() ?? "";
    return registryKey.includes(expected) || actionKind === expected || nodeType === expected || testId.includes(expected) || text.includes(expected);
  }), target);
}

async function clickVisibleRunButton(page) {
  const candidates = [
    page.getByTestId("microflow-editor-run"),
    page.getByTestId("microflow-workbench-run"),
  ];
  for (const candidate of candidates) {
    if (await candidate.isVisible().catch(() => false)) {
      await candidate.waitFor({ state: "visible", timeout: 10_000 });
      await candidate.click();
      return;
    }
  }
  throw new Error("未找到可见的运行按钮。");
}

async function main() {
  const browser = await chromium.launch({
    executablePath: browserExecutablePath,
    headless: process.env.HEADED === "1" ? false : true
  });
  const context = await browser.newContext({ viewport: { width: 1440, height: 950 } });
  const page = await context.newPage();
  page.on("pageerror", error => {
    console.error(`[browser pageerror] ${error.message}`);
  });
  page.on("console", message => {
    if (["error", "warning"].includes(message.type())) {
      console.error(`[browser ${message.type()}] ${message.text()}`);
    }
  });
  const evidence = {
    studioUrl,
    runName,
    artifactsDir,
    createdThroughUi: false,
    nodeDraggedThroughUi: false,
    acceptance120Configured: false,
    acceptance120Returned120: false,
    editorVisible: false,
    nodePanelVisible: false,
    saveVisible: false,
    runVisible: false,
    traceVisible: false,
    validationBlockedRun: false,
    screenshots: []
  };

  try {
    await loginForBrowserContext(context, page.request);
    await page.goto(studioUrl, { waitUntil: "domcontentloaded" });
    await assertVisible(page.getByTestId("microflow-explorer-node-module-1496887137133477890").or(page.getByText("Sales").first()), "Studio 模块树可见");

    const microflowsNode = page.getByTestId("microflow-explorer-node-microflows-1496887137133477890").or(page.getByText("Microflows").first());
    await microflowsNode.click({ button: "right" });
    await page.getByTestId("microflow-explorer-menu-new-microflow").click();
    await assertVisible(page.getByTestId("microflow-create-modal"), "创建微流弹窗可见");
    await page.getByLabel("Name").first().fill(runName);
    const displayName = page.getByLabel("显示名称").first();
    if (await displayName.isVisible().catch(() => false)) {
      await displayName.fill(runName);
    }
    const moduleLocked = page.getByTestId("microflow-create-module-locked");
    if (await moduleLocked.isVisible().catch(() => false)) {
      const value = await moduleLocked.locator("input").inputValue().catch(() => "");
      if (!value || value.includes("缺少模块")) {
        throw new Error(`模块上下文锁定异常：${value || "<empty>"}`);
      }
      console.log(`PASS 创建上下文锁定模块：${value}`);
    }
    await page.getByTestId("microflow-create-submit").click();
    await page.getByTestId("microflow-resource-editor-host").waitFor({ state: "visible", timeout: 45_000 });
    evidence.createdThroughUi = true;
    evidence.editorVisible = true;
    evidence.screenshots.push(await screenshot(page, "01-created-editor"));

    const nodePanel = page.getByTestId("microflow-node-panel");
    const nodePanelRail = page.getByTestId("microflow-node-panel-rail");
    await page.waitForTimeout(500);
    if (!await nodePanel.isVisible().catch(() => false)) {
      if (await nodePanelRail.isVisible({ timeout: 10_000 }).catch(() => false)) {
        await nodePanelRail.click();
      } else {
        await page.keyboard.press(process.platform === "darwin" ? "Meta+K" : "Control+K").catch(() => undefined);
        const commandSearch = page.getByPlaceholder("Search commands");
        if (await commandSearch.isVisible({ timeout: 2_000 }).catch(() => false)) {
          await commandSearch.fill("toolbox");
          await page.keyboard.press("Enter");
        }
      }
    }
    await assertVisible(nodePanel, "节点面板可见");
    evidence.nodePanelVisible = true;
    await assertVisible(page.getByTestId("microflow-workbench-save"), "保存按钮可见");
    evidence.saveVisible = true;
    const editorRunButton = page.getByTestId("microflow-editor-run");
    const workbenchRunButton = page.getByTestId("microflow-workbench-run");
    const runButton = await editorRunButton.isVisible().catch(() => false) ? editorRunButton : workbenchRunButton;
    await assertVisible(runButton, "运行按钮可见");
    evidence.runVisible = true;

    const supportedTargets = [
      "startEvent", "endEvent", "decision", "objectTypeDecision", "merge", "loop", "break", "continue",
      "parallelGateway", "inclusiveGateway", "errorEvent", "retrieve", "createObject", "changeMembers",
      "commit", "delete", "rollback", "cast", "createList", "changeList", "listOperation", "aggregateList",
      "filterList", "sortList", "createVariable", "changeVariable", "callMicroflow", "restCall",
      "logMessage", "throwException"
    ];
    const searchInput = page.getByTestId("microflow-node-panel-search").locator("input");
    const missing = [];
    for (const target of supportedTargets) {
      await searchInput.fill(target);
      await page.waitForTimeout(300);
      if (!await findPanelItem(page, target)) {
        missing.push(target);
      }
    }
    await searchInput.fill("");
    if (missing.length > 0) {
      throw new Error(`节点面板缺少 supported 节点：${missing.join(", ")}`);
    }
    console.log(`PASS 节点面板 supported 目标节点可检索：${supportedTargets.length}`);

    await searchInput.fill("createVariable");
    await page.waitForTimeout(300);
    const dragSource = page.locator("[data-testid^='microflow-node-panel-item-']").filter({ hasText: "Create Variable" });
    const dragSourceCount = await dragSource.count();
    if (dragSourceCount !== 1) {
      throw new Error(`Create Variable 拖动源不唯一：${dragSourceCount}`);
    }
    const canvas = page.getByTestId("microflow-flowgram-canvas");
    await assertVisible(canvas, "微流画布可见");
    const beforeDragNodeCount = await page.locator("[data-testid^='microflow-node-']").count();
    await dragSource.dragTo(canvas, {
      targetPosition: { x: 520, y: 260 },
      force: true
    });
    await page.waitForTimeout(800);
    const afterDragNodeCount = await page.locator("[data-testid^='microflow-node-']").count();
    if (afterDragNodeCount <= beforeDragNodeCount) {
      throw new Error(`拖动节点未落到画布：before=${beforeDragNodeCount}, after=${afterDragNodeCount}`);
    }
    evidence.nodeDraggedThroughUi = true;
    evidence.screenshots.push(await screenshot(page, "02-dragged-create-variable"));
    console.log(`PASS 真实拖动节点到画布：${beforeDragNodeCount} -> ${afterDragNodeCount}`);

    const acceptanceButton = page.getByTestId("microflow-workbench-acceptance-120");
    await assertVisible(acceptanceButton, "验收120入口可见");
    await acceptanceButton.click();
    await page.getByText("已配置并保存全节点验收计算图", { exact: false }).waitFor({ state: "visible", timeout: 45_000 });
    evidence.acceptance120Configured = true;
    evidence.screenshots.push(await screenshot(page, "03-acceptance-120-configured"));

    await page.getByText("校验中", { exact: false }).waitFor({ state: "detached", timeout: 45_000 }).catch(() => undefined);
    await clickVisibleRunButton(page);
    evidence.screenshots.push(await screenshot(page, "04-run-modal-open-attempt"));
    const runModal = page.getByTestId("microflow-test-run-modal-content");
    await assertVisible(runModal, "测试运行输入面板可见");
    const numbersInput = page.getByTestId("microflow-test-run-parameter-numbers").locator("textarea");
    await numbersInput.fill("[1,2,3,4,5,6]");
    const expectedResult = page.getByPlaceholder("期望结果 JSON，可留空");
    if (await expectedResult.isVisible().catch(() => false)) {
      await expectedResult.fill("120");
    }
    await page.getByTestId("microflow-test-run-submit").click();
    evidence.screenshots.push(await screenshot(page, "05-after-run-click"));
    const tracePanel = page.getByTestId("microflow-trace-panel");
    const succeededStatus = page.getByText("Succeeded").or(page.getByText("success")).or(page.getByText("120")).first();
    const runOutcome = await tracePanel.waitFor({ state: "attached", timeout: 5_000 })
      .then(() => "trace")
      .catch(async () => {
        await succeededStatus.waitFor({ state: "visible", timeout: 45_000 });
        return "result";
      });
    evidence.traceVisible = runOutcome === "trace";
    const pageText = await page.locator("body").innerText({ timeout: 10_000 });
    if (!pageText.includes("120")) {
      throw new Error("验收120运行后页面未显示返回值 120。");
    }
    evidence.acceptance120Returned120 = true;
    evidence.screenshots.push(await screenshot(page, "06-acceptance-120-run"));
    console.log("PASS 验收120通过真实浏览器输入 JSON 并显示返回值 120");

    writeFileSync(resolve(artifactsDir, `${runName}.json`), JSON.stringify(evidence, null, 2), "utf8");
    console.log(JSON.stringify(evidence, null, 2));
  } finally {
    await context.close();
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
