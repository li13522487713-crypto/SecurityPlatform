import { useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { MendixStudioApp, MendixStudioIndexPage } from "@atlas/mendix-studio-core";
import { getAccessToken } from "@atlas/shared-react-core/utils";
import { useWorkspaceContext } from "../workspace-context";
import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";
import { useAuth } from "../auth-context";

export function MendixStudioIndexRoute() {
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  return (
    <MendixStudioIndexPage
      workspaceId={workspace.id}
      onOpen={appId => navigate(`/space/${encodeURIComponent(workspace.id)}/mendix-studio/${encodeURIComponent(appId)}`)}
    />
  );
}

export function MendixStudioAppRoute() {
  const { appId = "" } = useParams<{ appId: string }>();
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
  const adapterConfig = useMemo(
    () => {
      const accessToken = getAccessToken();
      return createAppMicroflowAdapterConfig({
        workspaceId: workspace.id,
        tenantId: workspace.orgId,
        currentUser,
        requestHeaders: accessToken ? { Authorization: `Bearer ${accessToken}` } : undefined,
      });
    },
    [currentUser, workspace.id, workspace.orgId]
  );
  return (
    <MendixStudioApp
      appId={appId}
      workspaceId={workspace.id}
      tenantId={workspace.orgId}
      currentUser={currentUser}
      adapterConfig={adapterConfig}
    />
  );
}
