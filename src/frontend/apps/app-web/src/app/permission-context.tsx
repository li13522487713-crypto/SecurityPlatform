import { createContext, useContext, useMemo, type ReactNode } from "react";
import { useAuth } from "./auth-context";
import { useOptionalWorkspaceContext } from "./workspace-context";

export type WorkspaceRole = "workspace_owner" | "workspace_admin" | "workspace_editor" | "workspace_viewer";

export interface UserPermissionContext {
  userId: string;
  workspaceId?: string;
  workspaceRole?: WorkspaceRole;
  permissions: string[];
  hasPermission: (permission?: string | string[]) => boolean;
  hasAction: (action: string) => boolean;
}

const PermissionContext = createContext<UserPermissionContext | null>(null);

const ROLE_ALIAS_MAP: Record<string, WorkspaceRole> = {
  Owner: "workspace_owner",
  owner: "workspace_owner",
  workspace_owner: "workspace_owner",
  Admin: "workspace_admin",
  admin: "workspace_admin",
  workspace_admin: "workspace_admin",
  Member: "workspace_editor",
  member: "workspace_editor",
  workspace_editor: "workspace_editor",
  Viewer: "workspace_viewer",
  viewer: "workspace_viewer",
  workspace_viewer: "workspace_viewer"
};

function normalizeWorkspaceRole(roleCode?: string | null): WorkspaceRole | undefined {
  if (!roleCode) {
    return undefined;
  }
  return ROLE_ALIAS_MAP[roleCode.trim()];
}

/**
 * 把 AuthContext / WorkspaceContext 派生为 PRD（07-前端路由表）要求的 UserPermissionContext。
 *
 * 仅做派生与合并，不做 IO。允许在没有 WorkspaceProvider 的纯平台路由中也使用。
 */
export function PermissionProvider({ children }: { children: ReactNode }) {
  const auth = useAuth();
  const workspace = useOptionalWorkspaceContext();

  const value = useMemo<UserPermissionContext>(() => {
    const workspaceRole = normalizeWorkspaceRole(workspace?.roleCode);
    const allowedActions = workspace?.allowedActions ?? [];
    const allActions = new Set<string>(allowedActions);

    const hasAction = (action: string) => {
      if (!action) {
        return true;
      }
      if (workspaceRole === "workspace_owner" || workspaceRole === "workspace_admin") {
        return true;
      }
      return allActions.has(action);
    };

    const hasPermission = (permission?: string | string[]) => {
      if (!permission) {
        return true;
      }
      const list = Array.isArray(permission) ? permission : [permission];
      return list.every(item => auth.hasPermission(item));
    };

    return {
      userId: auth.profile?.id ?? "",
      workspaceId: workspace?.id,
      workspaceRole,
      permissions: auth.permissions,
      hasPermission,
      hasAction
    };
  }, [auth, workspace?.id, workspace?.roleCode, workspace?.allowedActions]);

  return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
}

export function usePermissionContext(): UserPermissionContext {
  const context = useContext(PermissionContext);
  if (!context) {
    throw new Error("PermissionProvider is missing.");
  }
  return context;
}

export function useOptionalPermissionContext(): UserPermissionContext | null {
  return useContext(PermissionContext);
}
