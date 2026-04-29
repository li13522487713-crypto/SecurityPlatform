import { MendixMicroflowResourceTab, createMicroflowEditorPath } from "@atlas/mendix-studio-core";
import { getAccessToken } from "@atlas/shared-react-core/utils";
import { useMemo } from "react";
import { useNavigate } from "react-router-dom";

import { useAuth } from "../auth-context";
import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";
import { useWorkspaceContext } from "../workspace-context";

export function MicroflowResourceTab() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const auth = useAuth();
  const currentUser = useMemo(() => {
    if (!auth.profile) {
      return undefined;
    }
    return {
      id: auth.profile.id,
      name: auth.profile.displayName || auth.profile.username,
      roles: auth.profile.roles
    };
  }, [auth.profile]);
  const adapterConfig = useMemo(() => {
    const accessToken = getAccessToken();
    return createAppMicroflowAdapterConfig({
      workspaceId: workspace.id,
      tenantId: workspace.orgId,
      currentUser,
      requestHeaders: accessToken ? { Authorization: `Bearer ${accessToken}` } : undefined,
    });
  }, [currentUser, workspace.id, workspace.orgId]);

  return (
    <MendixMicroflowResourceTab
      workspaceId={workspace.id}
      tenantId={workspace.orgId}
      currentUser={currentUser}
      adapterConfig={adapterConfig}
      onOpenMicroflow={resourceId => navigate(createMicroflowEditorPath(resourceId))}
    />
  );
}
