import { MendixMicroflowEditorPage, createMicroflowLibraryPath } from "@atlas/mendix-studio-core";
import { useNavigate, useParams } from "react-router-dom";

import { useWorkspaceContext } from "../workspace-context";

export function MicroflowEditorPage() {
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const { microflowId = "" } = useParams<{ microflowId: string }>();

  return (
    <MendixMicroflowEditorPage
      resourceId={microflowId}
      workspaceId={workspace.id}
      onBack={() => navigate(createMicroflowLibraryPath(workspace.id))}
    />
  );
}
