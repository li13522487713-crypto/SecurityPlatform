import { useMemo } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { MendixStudioApp, MendixStudioIndexPage } from "@atlas/mendix-studio-core";
import { useWorkspaceContext } from "../workspace-context";
import { createAppMicroflowAdapterConfig } from "../microflow-adapter-config";

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
  const adapterConfig = useMemo(
    () =>
      createAppMicroflowAdapterConfig({
        workspaceId: workspace.id,
        tenantId: workspace.orgId,
      }),
    [workspace.id, workspace.orgId]
  );
  return (
    <MendixStudioApp
      appId={appId}
      workspaceId={workspace.id}
      tenantId={workspace.orgId}
      adapterConfig={adapterConfig}
    />
  );
}
