import { useNavigate, useParams } from "react-router-dom";
import { MendixStudioApp, MendixStudioIndexPage } from "@atlas/mendix-studio-core";
import { useWorkspaceContext } from "../workspace-context";

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
  return <MendixStudioApp appId={appId} />;
}
