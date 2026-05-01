#!/usr/bin/env node
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const frontendRequire = createRequire(resolve(__dirname, "../src/frontend/package.json"));
const { chromium } = frontendRequire("@playwright/test");

const apiBaseUrl = (process.env.MICROFLOW_API_BASE_URL ?? "http://localhost:5002/api/v1").replace(/\/+$/u, "");
const studioBaseUrl = process.env.STUDIO_URL_BASE ?? "http://localhost:5181";
const tenantId = process.env.MICROFLOW_TENANT_ID ?? "00000000-0000-0000-0000-000000000001";
const username = process.env.MICROFLOW_USERNAME ?? "admin";
const password = process.env.MICROFLOW_PASSWORD ?? "P@ssw0rd!";
const workspaceId = process.env.MICROFLOW_WORKSPACE_ID ?? "1496769226340306944";
const appId = process.env.MICROFLOW_APP_ID ?? "1497140099043823616";
const artifactsDir = resolve(process.cwd(), "artifacts/domain-model-browser-e2e");

function apiUrl(path) {
  return `${apiBaseUrl}${path.startsWith("/") ? path : `/${path}`}`;
}

async function login(request) {
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
  return data;
}

async function api(request, token, method, path, body) {
  const response = await request.fetch(apiUrl(path), {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
      ...(body ? { "Content-Type": "application/json" } : {})
    },
    data: body
  });
  if (!response.ok()) {
    throw new Error(`${method} ${path} 失败：HTTP ${response.status()} ${await response.text()}`);
  }
  const payload = await response.json();
  return payload.data ?? payload;
}

async function screenshot(page, name) {
  mkdirSync(artifactsDir, { recursive: true });
  const file = resolve(artifactsDir, `${name}.png`);
  await page.screenshot({ path: file, fullPage: true });
  return file;
}

async function selectSemiOption(page, trigger, label) {
  await trigger.click();
  const option = page.locator(".semi-select-option").filter({ hasText: label }).first();
  await option.waitFor({ state: "visible", timeout: 15000 });
  await option.click();
}

async function main() {
  const browser = await chromium.launch({ headless: process.env.HEADED === "1" ? false : true });
  const context = await browser.newContext({ viewport: { width: 1600, height: 960 } });
  const page = await context.newPage();
  page.on("pageerror", error => console.error(`[pageerror] ${error.message}`));
  page.on("console", message => {
    if (["error", "warning"].includes(message.type())) {
      console.error(`[browser ${message.type()}] ${message.text()}`);
    }
  });

  const evidence = {
    workspaceId,
    appId,
    moduleId: "",
    sourceId: "",
    schemaName: "",
    importedTableName: "",
    createdEntityName: "",
    screenshots: []
  };

  try {
    const auth = await login(page.request);
    await context.addInitScript(({ accessToken, refreshToken, tenantId, workspaceId }) => {
      window.sessionStorage.setItem("atlas_app_access_token", accessToken);
      window.localStorage.setItem("atlas_app_refresh_token", refreshToken ?? "");
      window.localStorage.setItem("atlas_app_tenant_id", tenantId);
      window.localStorage.setItem("atlas_last_workspace_id", workspaceId);
      window.localStorage.setItem("atlas_locale", "zh-CN");
    }, {
      accessToken: auth.accessToken,
      refreshToken: auth.refreshToken,
      tenantId,
      workspaceId
    });

    const modules = await api(page.request, auth.accessToken, "GET", `/microflow-apps/${appId}/modules?workspaceId=${workspaceId}`);
    const module = modules[0];
    if (!module?.moduleId) {
      throw new Error("未找到可用模块，无法打开 Domain Model。");
    }
    evidence.moduleId = module.moduleId;

    const sourcesPayload = await api(page.request, auth.accessToken, "GET", `/database-center/sources?pageIndex=1&pageSize=100&workspaceId=${workspaceId}`);
    const source = (sourcesPayload.items ?? [])[0];
    if (!source?.id) {
      throw new Error("当前工作区没有可绑定数据库资源。");
    }
    evidence.sourceId = source.id;

    const schemas = await api(page.request, auth.accessToken, "GET", `/database-center/sources/${encodeURIComponent(source.id)}/schemas?environment=Draft`);
    const schemaName = schemas[0]?.name ?? "main";
    evidence.schemaName = schemaName;

    const importedTableName = `dm_e2e_import_${Date.now()}`;
    evidence.importedTableName = importedTableName;
    try {
      await api(
        page.request,
        auth.accessToken,
        "POST",
        `/database-center/sources/${encodeURIComponent(source.id)}/schemas/${encodeURIComponent(schemaName)}/structure/tables`,
        {
          schema: schemaName,
          tableName: importedTableName,
          comment: "Domain Model E2E import table",
          columns: [
            { name: "id", dataType: "TEXT", nullable: false, primaryKey: true },
            { name: "name", dataType: "TEXT", nullable: false }
          ],
          mode: "visual"
        }
      );
    } catch (error) {
      if (!String(error).includes("已存在")) {
        throw error;
      }
    }

    const studioUrl = `${studioBaseUrl}/space/${workspaceId}/mendix-studio/${appId}?panel=domainModel&moduleId=${module.moduleId}`;
    await page.goto(studioUrl, { waitUntil: "domcontentloaded" });
    await page.getByTestId("domain-model-workbench").waitFor({ state: "visible", timeout: 45000 });
    evidence.screenshots.push(await screenshot(page, "01-domain-model-open"));

    await page.getByTestId("domain-model-bind-open").click();
    await page.getByTestId("domain-model-bind-sheet").waitFor({ state: "visible", timeout: 15000 });
    await selectSemiOption(page, page.getByTestId("domain-model-bind-sheet").locator(".semi-select").first(), source.name);
    await page.getByLabel("别名").fill("e2edb");
    await page.getByTestId("domain-model-bind-submit").click();
    await page.getByTestId(`domain-model-binding-binding:1`).waitFor({ state: "visible", timeout: 20000 });

    await page.getByTestId("domain-model-import-open").click();
    await page.getByTestId("domain-model-import-sheet").waitFor({ state: "visible", timeout: 15000 });
    const importSheet = page.getByTestId("domain-model-import-sheet");
    await selectSemiOption(page, importSheet.locator(".semi-select").nth(0), "e2edb");
    await selectSemiOption(page, importSheet.locator(".semi-select").nth(1), schemaName);
    await selectSemiOption(page, importSheet.locator(".semi-select").nth(2), importedTableName);
    await page.getByTestId("domain-model-import-submit").click();
    await page.getByTestId(new RegExp(`domain-model-entity-card-entity:binding:1:${schemaName}:${importedTableName}`)).waitFor({ state: "visible", timeout: 25000 });

    const createdEntityName = `E2E_${Date.now()}`;
    evidence.createdEntityName = createdEntityName;
    await page.getByTestId("domain-model-create-entity-open").click();
    await page.getByTestId("domain-model-entity-modal").waitFor({ state: "visible", timeout: 15000 });
    await page.getByLabel("实体名称").fill(createdEntityName);
    await selectSemiOption(page, page.getByTestId("domain-model-entity-modal").locator(".semi-select").first(), "e2edb");
    await page.getByTestId("domain-model-entity-modal").getByRole("button", { name: "确定" }).click();
    await page.getByTestId("domain-model-save").click();

    await page.getByTestId("domain-model-preview-sync").click();
    await page.getByTestId("domain-model-preview-modal").waitFor({ state: "visible", timeout: 20000 });
    const previewText = await page.getByTestId("domain-model-preview-modal").textContent();
    if (!previewText?.includes("createTables: 1")) {
      throw new Error(`预览同步没有出现建表计划，实际内容：${previewText ?? "<empty>"}`);
    }
    evidence.screenshots.push(await screenshot(page, "02-domain-model-preview"));
    await page.keyboard.press("Escape");

    await page.getByTestId("domain-model-sync-draft").click();
    await page.getByTestId("domain-model-preview-panel").waitFor({ state: "visible", timeout: 30000 });
    await page.getByTestId("domain-model-refresh-metadata").click();
    evidence.screenshots.push(await screenshot(page, "03-domain-model-synced"));

    const structure = await api(
      page.request,
      auth.accessToken,
      "GET",
      `/database-center/sources/${encodeURIComponent(source.id)}/schemas/${encodeURIComponent(schemaName)}/structure`
    );
    const normalizedCreatedTable = createdEntityName.replace(/\s+/gu, "_").toLowerCase();
    if (!(structure.objects ?? []).some(item => item.name === normalizedCreatedTable)) {
      throw new Error(`同步 Draft 后未在数据库结构中找到新表 ${normalizedCreatedTable}。`);
    }

    const metadata = await api(
      page.request,
      auth.accessToken,
      "POST",
      `/microflow-apps/${appId}/domain-model/modules/${module.moduleId}/refresh-metadata?workspaceId=${workspaceId}`
    );
    if (!(metadata.entities ?? []).some(item => String(item.qualifiedName ?? "").includes(normalizedCreatedTable))) {
      throw new Error(`刷新 metadata 后未找到实体 ${normalizedCreatedTable}。`);
    }

    mkdirSync(artifactsDir, { recursive: true });
    writeFileSync(resolve(artifactsDir, "domain-model-browser-e2e.json"), JSON.stringify(evidence, null, 2), "utf8");
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
