import { createServer, type IncomingMessage, type Server, type ServerResponse } from "node:http";
import { writeFileSync } from "node:fs";
import { expect, test, type Locator, type Page } from "@playwright/test";
import { appBaseUrl, clearAuthStorage, ensureAppSetup, loginApp } from "./helpers";

const mockLlmPort = 19134;

async function readBody(request: IncomingMessage): Promise<string> {
  return await new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    request.on("data", (chunk) => {
      chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    });
    request.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    request.on("error", reject);
  });
}

function writeJson(response: ServerResponse, statusCode: number, payload: unknown): void {
  response.statusCode = statusCode;
  response.setHeader("Content-Type", "application/json; charset=utf-8");
  response.end(JSON.stringify(payload));
}

async function createMockLlmServer(): Promise<{ server: Server | null; owned: boolean }> {
  const server = createServer(async (request, response) => {
    if (request.method !== "POST" || request.url !== "/v1/chat/completions") {
      writeJson(response, 404, { error: "not_found" });
      return;
    }

    const rawBody = await readBody(request);
    let parsedBody: { model?: string; messages?: Array<{ content?: string }> } = {};
    try {
      parsedBody = rawBody ? (JSON.parse(rawBody) as { model?: string; messages?: Array<{ content?: string }> }) : {};
    } catch {
      writeJson(response, 400, { error: "invalid_json" });
      return;
    }

    const prompt = (parsedBody.messages ?? [])
      .map((item) => String(item.content ?? "").trim())
      .filter((item) => item.length > 0)
      .join("\n");
    const content = `mock-llm-response: ${prompt || "empty_prompt"}`;

    writeJson(response, 200, {
      id: "chatcmpl-mock-e2e",
      object: "chat.completion",
      model: parsedBody.model ?? "llama3",
      choices: [
        {
          index: 0,
          message: {
            role: "assistant",
            content
          },
          finish_reason: "stop"
        }
      ],
      usage: {
        prompt_tokens: 12,
        completion_tokens: 8,
        total_tokens: 20
      }
    });
  });

  return await new Promise((resolve, reject) => {
    const onError = (error: NodeJS.ErrnoException) => {
      reject(error);
    };

    server.once("error", onError);
    server.listen(mockLlmPort, "127.0.0.1", () => {
      server.removeListener("error", onError);
      resolve({ server, owned: true });
    });
  });
}

async function connectNodes(page: Page, fromNode: Locator, toNode: Locator): Promise<void> {
  const sourceHandle = fromNode.locator(".node-handle.node-handle-out.connectable").first();
  const targetHandle = toNode.locator(".node-handle.node-handle-in.connectable").first();

  await expect(sourceHandle).toBeVisible();
  await expect(targetHandle).toBeVisible();
  await sourceHandle.dragTo(targetHandle);
  await page.waitForTimeout(150);
}

test.describe.serial("Workflow Complete Flow", () => {
  let appKey = "";
  let mockLlmServer: Server | null = null;
  let ownsMockServer = false;

  test.beforeAll(async ({ request }) => {
    const mockServer = await createMockLlmServer();
    mockLlmServer = mockServer.server;
    ownsMockServer = mockServer.owned;
    appKey = await ensureAppSetup(request);
  });

  test.afterAll(async () => {
    if (!ownsMockServer || !mockLlmServer) {
      return;
    }

    await new Promise<void>((resolve, reject) => {
      mockLlmServer!.close((error) => {
        if (error) {
          reject(error);
          return;
        }
        resolve();
      });
    });
  });

  test("应完成工作流端到端链路（拖拽、拉线、模型回复）", async ({ page }, testInfo) => {
    test.setTimeout(360_000);

    await clearAuthStorage(page);
    await loginApp(page, appKey);
    await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows`);
    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows`), { timeout: 30_000 });

    const createResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "POST" && /\/api\/v2\/workflows$/.test(response.url());
    });

    await page.getByRole("button", { name: /新建工作流|Create Workflow/ }).click();
    const createResponse = await createResponsePromise;
    expect(createResponse.ok()).toBeTruthy();

    const createPayload = (await createResponse.json()) as { data?: { id?: string } | string };
    const createdWorkflowId =
      typeof createPayload.data === "string"
        ? createPayload.data
        : (createPayload.data?.id ?? "");

    await page.waitForTimeout(500);
    if (!/\/workflows\/[^/]+\/editor$/.test(page.url())) {
      expect(createdWorkflowId).not.toBe("");
      await page.goto(`${appBaseUrl}/apps/${encodeURIComponent(appKey)}/workflows/${encodeURIComponent(createdWorkflowId)}/editor`);
    }

    await page.waitForURL(new RegExp(`/apps/${encodeURIComponent(appKey)}/workflows/[^/]+/editor`), { timeout: 30_000 });

    const canvas = page.locator(".workflow-canvas");
    await expect(canvas).toBeVisible();

    const importCanvas = {
      nodes: [
        {
          key: "entry_e2e",
          type: "Entry",
          title: "Entry",
          layout: { x: 80, y: 180, width: 160, height: 60 },
          configs: {},
          inputMappings: {}
        },
        {
          key: "llm_e2e",
          type: "Llm",
          title: "Llm",
          layout: { x: 420, y: 180, width: 160, height: 60 },
          configs: {
            provider: "ollama_e2e",
            model: "llama3",
            prompt: "Reply with exact text: workflow-e2e-ok",
            stream: false,
            outputKey: "llm_output"
          },
          inputMappings: {}
        },
        {
          key: "exit_e2e",
          type: "Exit",
          title: "Exit",
          layout: { x: 780, y: 180, width: 160, height: 60 },
          configs: {},
          inputMappings: {}
        }
      ],
      connections: [
        {
          fromNode: "llm_e2e",
          fromPort: "output",
          toNode: "exit_e2e",
          toPort: "input",
          condition: null
        }
      ]
    };
    const importCanvasPath = testInfo.outputPath("workflow-import-canvas.json");
    writeFileSync(importCanvasPath, JSON.stringify(importCanvas, null, 2), "utf8");

    const fileChooserPromise = page.waitForEvent("filechooser");
    await page.getByRole("button", { name: /更多操作|More Actions/ }).click();
    await page.getByRole("menuitem", { name: /导入.*JSON|Import.*JSON/ }).click();
    const fileChooser = await fileChooserPromise;
    await fileChooser.setFiles(importCanvasPath);

    const entryNode = page.locator(".workflow-canvas .vue-flow__node", {
      has: page.locator(".node-type-entry")
    }).first();
    const llmNode = page.locator(".workflow-canvas .vue-flow__node", {
      has: page.locator(".node-type-llm")
    }).first();
    const exitNode = page.locator(".workflow-canvas .vue-flow__node", {
      has: page.locator(".node-type-exit")
    }).first();

    await expect(entryNode).toBeVisible();
    await expect(llmNode).toBeVisible();
    await expect(exitNode).toBeVisible();

    const edgeCountBeforeConnect = await page.locator(".workflow-canvas .vue-flow__edge").count();
    await connectNodes(page, entryNode, llmNode);

    await expect.poll(async () => page.locator(".workflow-canvas .vue-flow__edge").count(), {
      timeout: 10_000
    }).toBeGreaterThanOrEqual(edgeCountBeforeConnect + 1);

    const saveDraftResponsePromise = page.waitForResponse((response) => {
      return response.request().method() === "PUT" && /\/api\/v2\/workflows\/[^/]+\/draft$/.test(response.url());
    });

    await page.getByRole("button", { name: /保存草稿|Save Draft/ }).click();
    const saveDraftResponse = await saveDraftResponsePromise;
    expect(saveDraftResponse.ok()).toBeTruthy();

    await page.getByRole("button", { name: /测试运行|Test Run/ }).click();
    const testRunPanel = page.locator(".test-run-panel");
    await expect(testRunPanel).toBeVisible();

    await testRunPanel.locator("textarea").first().fill("{}");
    await testRunPanel.getByRole("button", { name: /流式运行|Stream Run/ }).click();
    await expect(testRunPanel.locator(".log-event .type-llm_output")).toContainText(/mock-llm-response/i, { timeout: 180_000 });
    await expect(testRunPanel.locator(".log-event .type-execution_complete")).toBeVisible({ timeout: 180_000 });
    await expect(testRunPanel.locator(".output-pre")).toContainText(/mock-llm-response/i, { timeout: 180_000 });
  });
});
