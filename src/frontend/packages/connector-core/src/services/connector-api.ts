import type { ApiResponse } from "@atlas/shared-kernel";
import type { RequestOptions } from "@atlas/shared-core/api";

export type ConnectorOnlineStatus = "online" | "degraded" | "offline";
export type ConnectorLogStatus = "pending" | "acknowledged" | "succeeded" | "failed";

export interface ConnectorRecord {
  id: number;
  name: string;
  baseUrl: string;
  authType: string;
  authConfig?: string | null;
  openApiSpecUrl?: string | null;
  healthCheckUrl?: string | null;
  timeoutSeconds: number;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface ConnectorOperation {
  id: number;
  connectorId: number;
  operationId: string;
  method: string;
  path: string;
  description?: string | null;
  requestSchema?: string | null;
  responseSchema?: string | null;
}

export interface ConnectorOnlineAppSummary {
  appKey: string;
  appName: string;
  status: ConnectorOnlineStatus;
  lastHeartbeatAt: string;
  capabilityCount: number;
}

export interface ConnectorCommandLogEntry {
  id: string;
  appKey: string;
  commandType: string;
  status: ConnectorLogStatus;
  createdAt: string;
  finishedAt?: string;
  message?: string;
  statusCode?: number;
  durationMs?: number;
  traceId?: string;
}

export interface ConnectorDispatchInput {
  connectorId: number;
  commandType: string;
  payload?: string | null;
  requestOptions?: RequestOptions;
  idempotencyKey?: string;
}

interface ConnectorServiceContext {
  requestApi: <T>(path: string, init?: RequestInit, options?: RequestOptions) => Promise<T>;
}

interface ConnectorCommandErrorContext {
  appKey: string;
  commandType: string;
  message: string;
}

interface ConnectorExecutionResult {
  success: boolean;
  statusCode: number;
  responseBody: string | null;
  durationMs: number;
}

interface ConnectorHealthResponse {
  healthy: boolean;
}

const LOG_STORAGE_KEY = "atlas_connector_command_logs_v1";
const LOG_MAX_ENTRIES = 120;
const IDEMPOTENCY_PREFIX = "connector-command";

function parseTextResponse(message: unknown): string {
  if (message == null) return "";
  if (message instanceof Error) return message.message;
  if (typeof message === "string") return message;
  try {
    return JSON.stringify(message);
  } catch {
    return String(message);
  }
}

function resolveRequestData<T>(response: ApiResponse<T>): T {
  if (!response?.success) {
    throw new Error(response?.message || "Request failed");
  }
  if (response.data === undefined) {
    throw new Error("响应数据为空");
  }
  return response.data as T;
}

function now() {
  return new Date().toISOString();
}

function createLogId(): string {
  const suffix = typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
    ? crypto.randomUUID()
    : Math.random().toString(16).slice(2);
  return `${IDEMPOTENCY_PREFIX}-${Date.now()}-${suffix}`;
}

function createConnectorCommandIdempotencyKey(input: ConnectorDispatchInput): string {
  const payloadSummary = (input.payload ?? "").slice(0, 40).replace(/\s+/g, "");
  return `${IDEMPOTENCY_PREFIX}-${input.connectorId}-${input.commandType}-${payloadSummary || "default"}-${Date.now()}`;
}

function safeStorageRead(): Storage | null {
  if (typeof localStorage === "undefined") return null;
  return localStorage;
}

function safeStoreLogs(logs: ConnectorCommandLogEntry[]): void {
  const storage = safeStorageRead();
  if (!storage) return;
  try {
    storage.setItem(LOG_STORAGE_KEY, JSON.stringify(logs));
  } catch {
    // ignore persistence failure
  }
}

function parseStoredLogs(raw: string | null): ConnectorCommandLogEntry[] {
  if (!raw) return [];
  try {
    const parsed = JSON.parse(raw) as ConnectorCommandLogEntry[];
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((item): item is ConnectorCommandLogEntry => {
      if (!item || typeof item !== "object") return false;
      if (!("id" in item) || !("appKey" in item) || !("commandType" in item)) return false;
      return true;
    });
  } catch {
    return [];
  }
}

export function getConnectorCommandHistory(): ConnectorCommandLogEntry[] {
  const storage = safeStorageRead();
  if (!storage) return [];
  return parseStoredLogs(storage.getItem(LOG_STORAGE_KEY));
}

export function appendConnectorCommandLog(entry: ConnectorCommandLogEntry): ConnectorCommandLogEntry[] {
  const logs = getConnectorCommandHistory().filter((item) => item.id !== entry.id);
  logs.unshift(entry);
  if (logs.length > LOG_MAX_ENTRIES) {
    logs.length = LOG_MAX_ENTRIES;
  }
  safeStoreLogs(logs);
  return logs;
}

export function createConnectorCommandFailureLog(input: ConnectorCommandErrorContext): ConnectorCommandLogEntry {
  const timestamp = now();
  return {
    id: createLogId(),
    appKey: input.appKey,
    commandType: input.commandType,
    status: "failed",
    createdAt: timestamp,
    finishedAt: timestamp,
    message: input.message
  };
}

export function createConnectorRequestContext(context: ConnectorServiceContext): ConnectorServiceContext {
  return context;
}

export async function listConnectors(context: ConnectorServiceContext): Promise<ConnectorRecord[]> {
  const response = await context.requestApi<ApiResponse<ConnectorRecord[]>>("/connectors");
  return resolveRequestData(response);
}

export async function listConnectorOperations(
  context: ConnectorServiceContext,
  connectorId: number
): Promise<ConnectorOperation[]> {
  const response = await context.requestApi<ApiResponse<ConnectorOperation[]>>(`/connectors/${connectorId}/operations`);
  return resolveRequestData(response);
}

export async function fetchConnectorOnlineApps(context: ConnectorServiceContext): Promise<ConnectorOnlineAppSummary[]> {
  const connectors = await listConnectors(context);
  const rows = await Promise.all(
    connectors.map(async (connector) => {
      const detailTasks = await Promise.allSettled([
        context.requestApi<ApiResponse<ConnectorHealthResponse>>(`/connectors/${connector.id}/health`),
        context.requestApi<ApiResponse<ConnectorOperation[]>>(`/connectors/${connector.id}/operations`)
      ]);

      let status: ConnectorOnlineStatus = connector.isActive ? "offline" : "offline";
      let capabilityCount = 0;

      const healthTask = detailTasks[0];
      if (healthTask.status === "fulfilled") {
        try {
          const data = resolveRequestData(healthTask.value);
          status = data.healthy ? "online" : "offline";
        } catch {
          status = "degraded";
        }
      } else {
        status = connector.isActive ? "degraded" : "offline";
      }

      const operationsTask = detailTasks[1];
      if (operationsTask.status === "fulfilled") {
        try {
          capabilityCount = resolveRequestData(operationsTask.value).length;
        } catch {
          capabilityCount = 0;
        }
      }

      return {
        appKey: String(connector.id),
        appName: connector.name || `Connector-${connector.id}`,
        status,
        lastHeartbeatAt: now(),
        capabilityCount
      };
    })
  );

  return rows.sort((a, b) => a.appName.localeCompare(b.appName));
}

export async function executeConnectorCommand(
  context: ConnectorServiceContext,
  input: ConnectorDispatchInput
): Promise<ConnectorCommandLogEntry> {
  const payload = {
    pathParams: {},
    queryParams: {},
    body: input.payload ?? null
  };

  const response = await context.requestApi<ApiResponse<ConnectorExecutionResult>>(
    `/connectors/${input.connectorId}/operations/${encodeURIComponent(input.commandType)}/execute`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json; charset=utf-8" },
      body: JSON.stringify(payload)
    },
    {
      idempotencyKey: input.idempotencyKey ?? createConnectorCommandIdempotencyKey(input),
      ...input.requestOptions
    }
  );

  const result = resolveRequestData(response);
  const timestamp = now();
  return {
    id: createLogId(),
    appKey: String(input.connectorId),
    commandType: input.commandType,
    status: result.success ? "succeeded" : "failed",
    createdAt: timestamp,
    finishedAt: timestamp,
    statusCode: result.statusCode,
    durationMs: result.durationMs,
    message: result.responseBody ?? (result.success ? "指令执行成功" : "指令执行失败")
  };
}

export function ensureConnectorCommandType(input: string): string {
  return input.trim();
}

