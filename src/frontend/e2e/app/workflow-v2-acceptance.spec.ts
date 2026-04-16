import { expect, test, type APIRequestContext } from "../fixtures/single-session";
import { appApiBase, defaultPassword, defaultTenantId, defaultUsername, platformApiBase } from "./helpers";

const TENANT_HEADERS = {
  "X-Tenant-Id": defaultTenantId,
  "X-Project-Id": "1",
  "Content-Type": "application/json"
};

const NODE_TYPE = {
  Entry: 1,
  Exit: 2,
  Selector: 8,
  TextProcessor: 15,
  Break: 19,
  Loop: 21,
  Batch: 28,
  Continue: 29,
  InputReceiver: 30
} as const;

interface ApiResponse<T> {
  success?: boolean;
  code?: string;
  message?: string;
  data?: T;
}

interface AuthTokenData {
  accessToken?: string;
}

interface WorkflowCreateData {
  Id?: string;
  id?: string;
}

interface WorkflowRunData {
  executionId?: string;
  status?: number | string;
  outputsJson?: string | null;
}

interface WorkflowProcessData {
  status?: number | string;
}

interface WorkflowTraceStep {
  nodeKey?: string;
  status?: number | string;
  outputs?: Record<string, unknown>;
}

interface WorkflowTraceEdge {
  sourceNodeKey?: string;
  targetNodeKey?: string;
  status?: number | string;
  reason?: string | null;
}

interface WorkflowTraceData {
  executionId?: string;
  status?: number | string;
  steps?: WorkflowTraceStep[];
  edgeStatuses?: WorkflowTraceEdge[];
}

function createCanvas(
  nodes: Array<Record<string, unknown>>,
  connections: Array<Record<string, unknown>>,
  globals: Record<string, unknown> = {}
): string {
  return JSON.stringify({
    nodes,
    connections,
    schemaVersion: 2,
    viewport: {
      x: 0,
      y: 0,
      zoom: 100
    },
    globals
  });
}

function node(
  key: string,
  type: number,
  label: string,
  config: Record<string, unknown>,
  x: number,
  y: number,
  childCanvas?: Record<string, unknown>
): Record<string, unknown> {
  return {
    key,
    type,
    label,
    config,
    layout: {
      x,
      y,
      width: 320,
      height: 120
    },
    ...(childCanvas ? { childCanvas } : {})
  };
}

function isInterrupted(status: number | string | undefined): boolean {
  return status === 5 || String(status).toLowerCase() === "interrupted";
}

function isCompleted(status: number | string | undefined): boolean {
  return status === 2 || String(status).toLowerCase() === "completed";
}

async function getAppAccessToken(request: APIRequestContext): Promise<string> {
  const resp = await request.post(`${appApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });
  expect(resp.ok()).toBeTruthy();
  const payload = (await resp.json()) as ApiResponse<AuthTokenData>;
  const token = payload.data?.accessToken ?? "";
  expect(token).not.toBe("");
  return token;
}

async function getPlatformAccessToken(request: APIRequestContext): Promise<string> {
  const resp = await request.post(`${platformApiBase}/api/v1/auth/token`, {
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": defaultTenantId
    },
    data: {
      username: defaultUsername,
      password: defaultPassword
    }
  });
  expect(resp.ok()).toBeTruthy();
  const payload = (await resp.json()) as ApiResponse<AuthTokenData>;
  const token = payload.data?.accessToken ?? "";
  expect(token).not.toBe("");
  return token;
}

function authHeaders(accessToken: string): Record<string, string> {
  return {
    ...TENANT_HEADERS,
    Authorization: `Bearer ${accessToken}`
  };
}

async function createWorkflow(request: APIRequestContext, accessToken: string, name: string): Promise<string> {
  const resp = await request.post(`${appApiBase}/api/v2/workflows`, {
    headers: authHeaders(accessToken),
    data: {
      name,
      description: "workflow-core e2e",
      mode: 0
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<WorkflowCreateData>;
  expect(payload.success).toBeTruthy();
  const workflowId = payload.data?.Id ?? payload.data?.id ?? "";
  expect(workflowId).not.toBe("");
  return workflowId;
}

async function saveDraftCanvas(
  request: APIRequestContext,
  accessToken: string,
  workflowId: string,
  canvasJson: string
): Promise<void> {
  const resp = await request.put(`${appApiBase}/api/v2/workflows/${workflowId}/draft`, {
    headers: authHeaders(accessToken),
    data: {
      canvasJson,
      commitId: null
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<{ Id?: string; id?: string }>;
  expect(payload.success).toBeTruthy();
}

async function publishWorkflow(request: APIRequestContext, accessToken: string, workflowId: string): Promise<void> {
  const resp = await request.post(`${appApiBase}/api/v2/workflows/${workflowId}/publish`, {
    headers: authHeaders(accessToken),
    data: {
      changeLog: "e2e publish"
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<{ Id?: string; id?: string }>;
  expect(payload.success).toBeTruthy();
}

async function runWorkflow(
  request: APIRequestContext,
  accessToken: string,
  workflowId: string,
  options: {
    source: "draft" | "published";
    inputs: Record<string, unknown>;
  }
): Promise<ApiResponse<WorkflowRunData>> {
  const resp = await request.post(`${appApiBase}/api/v2/workflows/${workflowId}/run`, {
    headers: authHeaders(accessToken),
    data: {
      source: options.source,
      inputsJson: JSON.stringify(options.inputs)
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<WorkflowRunData>;
  expect(payload.success).toBeTruthy();
  expect(payload.data?.executionId ?? "").not.toBe("");
  return payload;
}

async function getTrace(
  request: APIRequestContext,
  accessToken: string,
  executionId: string
): Promise<ApiResponse<WorkflowTraceData>> {
  const resp = await request.get(`${appApiBase}/api/v2/workflows/executions/${executionId}/trace`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "X-Tenant-Id": defaultTenantId,
      "X-Project-Id": "1"
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<WorkflowTraceData>;
  expect(payload.success).toBeTruthy();
  return payload;
}

async function getProcess(
  request: APIRequestContext,
  accessToken: string,
  executionId: string
): Promise<ApiResponse<WorkflowProcessData>> {
  const resp = await request.get(`${appApiBase}/api/v2/workflows/executions/${executionId}/process`, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      "X-Tenant-Id": defaultTenantId,
      "X-Project-Id": "1"
    }
  });
  expect(resp.status()).toBe(200);
  const payload = (await resp.json()) as ApiResponse<WorkflowProcessData>;
  expect(payload.success).toBeTruthy();
  return payload;
}

test.describe.serial("@workflow-core Workflow V2 Core", () => {
  test("Selector 分支命中并标记未命中分支为 skipped", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const workflowId = await createWorkflow(request, token, `selector_core_${Date.now()}`);

    const canvasJson = createCanvas(
      [
        node("entry_1", NODE_TYPE.Entry, "Entry", { entryVariable: "USER_INPUT", entryAutoSaveHistory: true }, 80, 120),
        node("selector_1", NODE_TYPE.Selector, "Selector", { condition: 'input.route == "true"', logic: "and", conditions: [] }, 420, 120),
        node("true_text_1", NODE_TYPE.TextProcessor, "TrueText", { template: "true-branch", outputKey: "true_text" }, 760, 40),
        node("false_text_1", NODE_TYPE.TextProcessor, "FalseText", { template: "false-branch", outputKey: "false_text" }, 760, 220),
        node("exit_true_1", NODE_TYPE.Exit, "ExitTrue", { exitTerminateMode: "return", exitTemplate: "{{true_text}}" }, 1080, 40),
        node("exit_false_1", NODE_TYPE.Exit, "ExitFalse", { exitTerminateMode: "return", exitTemplate: "{{false_text}}" }, 1080, 220)
      ],
      [
        { sourceNodeKey: "entry_1", sourcePort: "output", targetNodeKey: "selector_1", targetPort: "input", condition: null },
        { sourceNodeKey: "selector_1", sourcePort: "true", targetNodeKey: "true_text_1", targetPort: "input", condition: "true" },
        { sourceNodeKey: "selector_1", sourcePort: "false", targetNodeKey: "false_text_1", targetPort: "input", condition: "false" },
        { sourceNodeKey: "true_text_1", sourcePort: "output", targetNodeKey: "exit_true_1", targetPort: "input", condition: null },
        { sourceNodeKey: "false_text_1", sourcePort: "output", targetNodeKey: "exit_false_1", targetPort: "input", condition: null }
      ]
    );

    await saveDraftCanvas(request, token, workflowId, canvasJson);
    await publishWorkflow(request, token, workflowId);
    const runPayload = await runWorkflow(request, token, workflowId, {
      source: "published",
      inputs: {
        input: {
          route: "true"
        }
      }
    });

    const executionId = runPayload.data?.executionId ?? "";
    const tracePayload = await getTrace(request, token, executionId);
    const steps = tracePayload.data?.steps ?? [];
    const edgeStatuses = tracePayload.data?.edgeStatuses ?? [];

    expect(steps.some((step) => step.nodeKey === "true_text_1" && isCompleted(step.status))).toBeTruthy();
    expect(steps.some((step) => step.nodeKey === "false_text_1" && (step.status === 6 || String(step.status).toLowerCase() === "skipped"))).toBeTruthy();
    expect(edgeStatuses.some((edge) => edge.sourceNodeKey === "selector_1" && edge.targetNodeKey === "false_text_1" && String(edge.reason ?? "").includes("selector_unselected_branch"))).toBeTruthy();
  });

  test("Loop + Break 回归，Continue 节点输出回归", async ({ request }) => {
    const token = await getAppAccessToken(request);

    const loopWorkflowId = await createWorkflow(request, token, `loop_break_core_${Date.now()}`);
    const loopCanvasJson = createCanvas(
      [
        node("entry_1", NODE_TYPE.Entry, "Entry", { entryVariable: "USER_INPUT", entryAutoSaveHistory: true }, 80, 140),
        node("loop_1", NODE_TYPE.Loop, "Loop", { mode: "count", maxIterations: 5, indexVariable: "loop_index" }, 420, 140),
        node("break_1", NODE_TYPE.Break, "Break", { reason: "break-on-first" }, 760, 140),
        node("exit_1", NODE_TYPE.Exit, "Exit", { exitTerminateMode: "return", exitTemplate: "{{loop_index}}" }, 1080, 140)
      ],
      [
        { sourceNodeKey: "entry_1", sourcePort: "output", targetNodeKey: "loop_1", targetPort: "input", condition: null },
        { sourceNodeKey: "loop_1", sourcePort: "body", targetNodeKey: "break_1", targetPort: "input", condition: "false" },
        { sourceNodeKey: "loop_1", sourcePort: "done", targetNodeKey: "exit_1", targetPort: "input", condition: "true" }
      ]
    );

    await saveDraftCanvas(request, token, loopWorkflowId, loopCanvasJson);
    const loopRunPayload = await runWorkflow(request, token, loopWorkflowId, {
      source: "draft",
      inputs: { input: { message: "loop" } }
    });

    const loopTracePayload = await getTrace(request, token, loopRunPayload.data?.executionId ?? "");
    const loopSteps = loopTracePayload.data?.steps ?? [];
    expect(loopSteps.some((step) => step.nodeKey === "loop_1" && isCompleted(step.status))).toBeTruthy();
    expect(loopSteps.some((step) => step.nodeKey === "break_1" && isCompleted(step.status))).toBeTruthy();

    const continueWorkflowId = await createWorkflow(request, token, `continue_core_${Date.now()}`);
    const continueCanvasJson = createCanvas(
      [
        node("entry_1", NODE_TYPE.Entry, "Entry", { entryVariable: "USER_INPUT", entryAutoSaveHistory: true }, 80, 140),
        node("continue_1", NODE_TYPE.Continue, "Continue", { remark: "continue-coverage" }, 420, 140),
        node("exit_1", NODE_TYPE.Exit, "Exit", { exitTerminateMode: "return", exitTemplate: "{{loop_continue}}" }, 760, 140)
      ],
      [
        { sourceNodeKey: "entry_1", sourcePort: "output", targetNodeKey: "continue_1", targetPort: "input", condition: null },
        { sourceNodeKey: "continue_1", sourcePort: "output", targetNodeKey: "exit_1", targetPort: "input", condition: null }
      ]
    );

    await saveDraftCanvas(request, token, continueWorkflowId, continueCanvasJson);
    const continueRunPayload = await runWorkflow(request, token, continueWorkflowId, {
      source: "draft",
      inputs: { input: { message: "continue" } }
    });

    const continueTracePayload = await getTrace(request, token, continueRunPayload.data?.executionId ?? "");
    const continueStep = (continueTracePayload.data?.steps ?? []).find((step) => step.nodeKey === "continue_1");
    expect(continueStep).toBeTruthy();
    expect(isCompleted(continueStep?.status)).toBeTruthy();
    expect(continueStep?.outputs?.loop_continue).toBeTruthy();
  });

  test("Batch 子图执行回归", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const workflowId = await createWorkflow(request, token, `batch_core_${Date.now()}`);

    const childCanvas = {
      nodes: [
        node("text_1", NODE_TYPE.TextProcessor, "ChildText", { template: "{{batch_item}}", outputKey: "item_text" }, 80, 80),
        node("exit_1", NODE_TYPE.Exit, "ChildExit", { exitTerminateMode: "return", exitTemplate: "{{item_text}}" }, 420, 80)
      ],
      connections: [
        { sourceNodeKey: "text_1", sourcePort: "output", targetNodeKey: "exit_1", targetPort: "input", condition: null }
      ],
      schemaVersion: 2,
      viewport: { x: 0, y: 0, zoom: 100 },
      globals: {}
    };

    const canvasJson = createCanvas(
      [
        node("entry_1", NODE_TYPE.Entry, "Entry", { entryVariable: "USER_INPUT", entryAutoSaveHistory: true }, 80, 160),
        node("batch_1", NODE_TYPE.Batch, "Batch", {
          concurrentSize: 2,
          batchSize: 2,
          inputArrayPath: "input.items",
          itemVariable: "batch_item",
          itemIndexVariable: "batch_item_index",
          outputKey: "batch_results"
        }, 420, 160, childCanvas),
        node("exit_1", NODE_TYPE.Exit, "Exit", { exitTerminateMode: "return", exitTemplate: "{{batch_results}}" }, 760, 160)
      ],
      [
        { sourceNodeKey: "entry_1", sourcePort: "output", targetNodeKey: "batch_1", targetPort: "input", condition: null },
        { sourceNodeKey: "batch_1", sourcePort: "output", targetNodeKey: "exit_1", targetPort: "input", condition: null }
      ]
    );

    await saveDraftCanvas(request, token, workflowId, canvasJson);
    const runPayload = await runWorkflow(request, token, workflowId, {
      source: "draft",
      inputs: {
        input: {
          items: ["alpha", "beta", "gamma"]
        }
      }
    });

    const tracePayload = await getTrace(request, token, runPayload.data?.executionId ?? "");
    const batchStep = (tracePayload.data?.steps ?? []).find((step) => step.nodeKey === "batch_1");
    expect(batchStep).toBeTruthy();
    expect(isCompleted(batchStep?.status)).toBeTruthy();

    const batchResults = (batchStep?.outputs?.batch_results as unknown[]) ?? [];
    expect(batchResults.length).toBe(3);
  });

  test("Resume 使用 preCompletedNodeKeys 恢复执行", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const workflowId = await createWorkflow(request, token, `resume_core_${Date.now()}`);

    const canvasJson = createCanvas(
      [
        node("entry_1", NODE_TYPE.Entry, "Entry", { entryVariable: "USER_INPUT", entryAutoSaveHistory: true }, 80, 120),
        node("input_receiver_1", NODE_TYPE.InputReceiver, "InputReceiver", { inputPath: "workflow_input", outputSchema: {} }, 420, 120),
        node("exit_1", NODE_TYPE.Exit, "Exit", { exitTerminateMode: "return", exitTemplate: "{{workflow_input}}" }, 760, 120)
      ],
      [
        { sourceNodeKey: "input_receiver_1", sourcePort: "output", targetNodeKey: "exit_1", targetPort: "input", condition: null }
      ]
    );

    await saveDraftCanvas(request, token, workflowId, canvasJson);

    const runPayload = await runWorkflow(request, token, workflowId, {
      source: "draft",
      inputs: {}
    });

    const executionId = runPayload.data?.executionId ?? "";

    await expect
      .poll(async () => {
        const processPayload = await getProcess(request, token, executionId);
        return isInterrupted(processPayload.data?.status);
      }, { timeout: 30_000 })
      .toBe(true);

    const resumeResp = await request.post(`${appApiBase}/api/v2/workflows/executions/${executionId}/resume`, {
      headers: authHeaders(token),
      data: {
        data: {
          workflow_input: "resume-value"
        }
      }
    });
    expect(resumeResp.status()).toBe(200);
    const resumePayload = (await resumeResp.json()) as ApiResponse<{ Id?: string; id?: string }>;
    expect(resumePayload.success).toBeTruthy();

    await expect
      .poll(async () => {
        const processPayload = await getProcess(request, token, executionId);
        return isCompleted(processPayload.data?.status);
      }, { timeout: 30_000 })
      .toBe(true);

    const tracePayload = await getTrace(request, token, executionId);
    const steps = tracePayload.data?.steps ?? [];
    const entrySteps = steps.filter((step) => step.nodeKey === "entry_1");
    expect(entrySteps.length).toBe(1);
    expect(steps.some((step) => step.nodeKey === "input_receiver_1" && isCompleted(step.status))).toBeTruthy();
  });

  test("失败链路：未发布 published 运行返回 400", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const workflowId = await createWorkflow(request, token, `published_guard_${Date.now()}`);

    const resp = await request.post(`${appApiBase}/api/v2/workflows/${workflowId}/run`, {
      headers: authHeaders(token),
      data: {
        source: "published",
        inputsJson: JSON.stringify({ input: { message: "published-guard" } })
      }
    });

    expect(resp.status()).toBe(400);
    const payload = (await resp.json()) as ApiResponse<unknown>;
    expect(payload.success).toBeFalsy();
  });

  test("失败链路：取消不存在执行返回 404", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const resp = await request.post(`${appApiBase}/api/v2/workflows/executions/999999999/cancel`, {
      headers: authHeaders(token),
      data: {}
    });

    expect(resp.status()).toBe(404);
    const payload = (await resp.json()) as ApiResponse<unknown>;
    expect(payload.success).toBeFalsy();
  });

  test("失败链路：查询不存在 trace 返回 404", async ({ request }) => {
    const token = await getAppAccessToken(request);
    const resp = await request.get(`${appApiBase}/api/v2/workflows/executions/999999999/trace`, {
      headers: {
        Authorization: `Bearer ${token}`,
        "X-Tenant-Id": defaultTenantId,
        "X-Project-Id": "1"
      }
    });

    expect(resp.status()).toBe(404);
    const payload = (await resp.json()) as ApiResponse<unknown>;
    expect(payload.success).toBeFalsy();
  });

  test("失败链路：权限校验返回 401/403", async ({ request }) => {
    const noTokenResp = await request.get(`${appApiBase}/api/v2/workflows`, {
      headers: {
        "X-Tenant-Id": defaultTenantId,
        "X-Project-Id": "1"
      }
    });
    expect(noTokenResp.status()).toBe(401);

    const platformToken = await getPlatformAccessToken(request);
    const wrongTokenResp = await request.get(`${appApiBase}/api/v2/workflows`, {
      headers: {
        Authorization: `Bearer ${platformToken}`,
        "X-Tenant-Id": defaultTenantId,
        "X-Project-Id": "1"
      }
    });
    expect([401, 403]).toContain(wrongTokenResp.status());
  });
});
