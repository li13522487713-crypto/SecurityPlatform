export interface ConnectorConfigSchema {
  connectorKey: string;
  enabled: boolean;
  authType: "none" | "token" | "oauth2" | "mtls";
}
