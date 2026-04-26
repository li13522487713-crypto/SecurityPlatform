import { MendixMicroflowResourceTab, createMicroflowEditorPath } from "@atlas/mendix-studio-core";
import { useNavigate } from "react-router-dom";

import { useWorkspaceContext } from "../workspace-context";

export function MicroflowResourceTab() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();

  return (
    <MendixMicroflowResourceTab
      workspaceId={workspace.id}
      currentUser={{ id: "current-user", name: "Current User" }}
      onOpenMicroflow={resourceId => navigate(createMicroflowEditorPath(resourceId))}
    />
  );
}
