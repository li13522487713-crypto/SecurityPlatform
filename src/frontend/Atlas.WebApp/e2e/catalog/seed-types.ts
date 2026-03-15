export type E2ERole =
  | "superadmin"
  | "sysadmin"
  | "securityadmin"
  | "approvaladmin"
  | "aiadmin"
  | "appadmin"
  | "deptadminA"
  | "deptadminB"
  | "userA"
  | "userB"
  | "readonly";

export type CatalogDomain =
  | "navigation"
  | "console"
  | "system"
  | "security"
  | "ai"
  | "approval"
  | "lowcode"
  | "monitor"
  | "compat";

export type CatalogPriority = "P0" | "P1" | "P2";

export type LandmarkType = "title" | "page-card" | "table" | "text" | "testid";

export interface ExpectedLandmark {
  type: LandmarkType;
  value?: string;
}

export interface SeedAccount {
  role: E2ERole;
  username: string;
  displayName: string;
  tenantId: string;
  homePath: string;
  roleCodes: string[];
}

export interface SeedState {
  generatedAt: string;
  baseTenantId: string;
  secondaryTenantId?: string;
  accounts: Record<E2ERole, SeedAccount>;
  departments: Record<string, string>;
  positions: Record<string, string>;
  roles: Record<string, string>;
  projects: Record<string, string>;
  dataSources: Record<string, string>;
  lowCodeApps: Record<string, string>;
  lowCodeAppKeys: Record<string, string>;
  pages: Record<string, string>;
  ai: {
    modelConfigId?: string;
    agentId?: string;
    knowledgeBaseId?: string;
    databaseId?: string;
    pluginId?: string;
    aiAppId?: string;
    variableId?: string;
    promptId?: string;
    shortcutId?: string;
    marketplaceId?: string;
  };
  approval: {
    flowId?: string;
    instanceId?: string;
    taskId?: string;
    copyId?: string;
  };
  notifications: {
    notificationId?: string;
  };
  dictionaries: {
    dictTypeId?: string;
  };
  configs: {
    systemConfigId?: string;
  };
  webhooks: {
    webhookId?: string;
  };
}

export interface StoredAuthState {
  role: E2ERole;
  username: string;
  tenantId: string;
  homePath: string;
  permissions: string[];
  roles: string[];
  cookies: Array<{
    name: string;
    value: string;
    domain: string;
    path: string;
    expires: number;
    httpOnly: boolean;
    secure: boolean;
    sameSite: "Strict" | "Lax" | "None";
  }>;
  localStorage: Record<string, string>;
  sessionStorage: Record<string, string>;
}

export interface CatalogEntry {
  id: string;
  domain: CatalogDomain;
  priority: CatalogPriority;
  loginRole: E2ERole;
  path: string | ((seed: SeedState) => string | null | undefined);
  expectedTitle?: string;
  landmark: ExpectedLandmark;
  successFlow: string;
  restrictedRole?: E2ERole;
  restrictedFlow?: string;
}

export interface RoleAccessRule {
  role: E2ERole;
  visibleMenuTestIds: string[];
  deniedPaths: string[];
}
