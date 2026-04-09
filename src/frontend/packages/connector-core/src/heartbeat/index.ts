export interface ConnectorHeartbeatState {
  appKey: string;
  status: "online" | "offline" | "degraded";
  lastSeenAt: string;
}
