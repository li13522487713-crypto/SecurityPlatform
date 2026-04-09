export type PermissionScope = "platform" | "app" | "tenant";

export interface PermissionCode {
  code: string;
  scope: PermissionScope;
  description?: string;
}

export interface PermissionSet {
  roles: string[];
  permissions: string[];
  isPlatformAdmin?: boolean;
}
