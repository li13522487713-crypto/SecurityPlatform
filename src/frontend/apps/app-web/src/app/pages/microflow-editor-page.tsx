import { MendixMicroflowEditorPage, createMicroflowLibraryPath } from "@atlas/mendix-studio-core";
import { getAccessToken } from "@atlas/shared-react-core/utils";
import { useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";

import { useAuth } from "../auth-context";
import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";
import { useWorkspaceContext } from "../workspace-context";

export function MicroflowEditorPage() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const { microflowId = "" } = useParams<{ microflowId: string }>();
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
    return createAppMicroflowAdapterConfig({
      workspaceId: workspace.id,
      tenantId: workspace.orgId,
      currentUser,
      requestHeaders: () => {
        const accessToken = getAccessToken();
        return accessToken ? { Authorization: `Bearer ${accessToken}` } : undefined;
      },
    });
  }, [currentUser, workspace.id, workspace.orgId]);

  return (
    <MendixMicroflowEditorPage
      resourceId={microflowId}
      workspaceId={workspace.id}
      adapterConfig={adapterConfig}
      onBack={() => navigate(createMicroflowLibraryPath(workspace.id))}
    />
  );
}
