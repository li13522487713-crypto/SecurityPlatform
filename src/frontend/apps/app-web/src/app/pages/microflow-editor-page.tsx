import { MendixMicroflowEditorPage, createMicroflowLibraryPath } from "@atlas/mendix-studio-core";
import { useNavigate, useParams } from "react-router-dom";

import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";
import { useWorkspaceContext } from "../workspace-context";

export function MicroflowEditorPage() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const { microflowId = "" } = useParams<{ microflowId: string }>();
  const currentUser = { id: import.meta.env.VITE_DEFAULT_USERNAME ?? "current-user", name: import.meta.env.VITE_DEFAULT_USERNAME ?? "Current User" };

  return (
    <MendixMicroflowEditorPage
      resourceId={microflowId}
      workspaceId={workspace.id}
      adapterConfig={createAppMicroflowAdapterConfig({
        workspaceId: workspace.id,
        tenantId: workspace.orgId,
        currentUser,
      })}
      onBack={() => navigate(createMicroflowLibraryPath(workspace.id))}
    />
  );
}
