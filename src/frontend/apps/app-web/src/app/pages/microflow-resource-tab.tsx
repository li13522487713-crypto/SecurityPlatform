import { MendixMicroflowResourceTab, createMicroflowEditorPath } from "@atlas/mendix-studio-core";
import { useNavigate } from "react-router-dom";

import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";
import { useWorkspaceContext } from "../workspace-context";

export function MicroflowResourceTab() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const currentUser = { id: import.meta.env.VITE_DEFAULT_USERNAME ?? "current-user", name: import.meta.env.VITE_DEFAULT_USERNAME ?? "Current User" };

  return (
    <MendixMicroflowResourceTab
      workspaceId={workspace.id}
      tenantId={workspace.orgId}
      currentUser={currentUser}
      adapterConfig={createAppMicroflowAdapterConfig({
        workspaceId: workspace.id,
        tenantId: workspace.orgId,
        currentUser,
      })}
      onOpenMicroflow={resourceId => navigate(createMicroflowEditorPath(resourceId))}
    />
  );
}
