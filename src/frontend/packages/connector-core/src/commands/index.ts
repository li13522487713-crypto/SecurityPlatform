export interface ConnectorCommand {
  commandType: string;
  payload?: Record<string, unknown>;
}
