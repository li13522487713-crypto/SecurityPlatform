export interface ConnectorOnlineAppSummary {
  appKey: string;
  appName: string;
  status: "online" | "degraded" | "offline";
  lastHeartbeatAt: string;
  capabilityCount: number;
}

export interface ConnectorCommandLogEntry {
  id: string;
  appKey: string;
  commandType: string;
  status: "pending" | "acknowledged" | "succeeded" | "failed";
  createdAt: string;
  finishedAt?: string;
  message?: string;
}

const onlineApps: ConnectorOnlineAppSummary[] = [
  {
    appKey: "asset-center",
    appName: "资产中心",
    status: "online",
    lastHeartbeatAt: new Date(Date.now() - 15_000).toISOString(),
    capabilityCount: 12,
  },
  {
    appKey: "workflow-hub",
    appName: "流程中心",
    status: "degraded",
    lastHeartbeatAt: new Date(Date.now() - 45_000).toISOString(),
    capabilityCount: 8,
  },
  {
    appKey: "ops-console",
    appName: "运维控制台",
    status: "offline",
    lastHeartbeatAt: new Date(Date.now() - 180_000).toISOString(),
    capabilityCount: 5,
  },
];

const commandLogs: ConnectorCommandLogEntry[] = [];

export function getMockOnlineApps() {
  return [...onlineApps];
}

export function getMockCommandLogs() {
  return [...commandLogs].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
}

export function dispatchMockCommand(input: {
  appKey: string;
  commandType: string;
  message?: string;
}) {
  const id = `cmd-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
  const entry: ConnectorCommandLogEntry = {
    id,
    appKey: input.appKey,
    commandType: input.commandType,
    status: "succeeded",
    createdAt: new Date().toISOString(),
    finishedAt: new Date().toISOString(),
    message: input.message ?? "已执行",
  };
  commandLogs.unshift(entry);
  return entry;
}
