import { MendixMicroflowResourceTab, createMicroflowEditorPath } from "@atlas/mendix-studio-core";
import { useNavigate } from "react-router-dom";

export function MicroflowDemoPage() {
  const navigate = useNavigate();

  return (
    <div style={{ height: "calc(100vh - 60px)", minHeight: 720, padding: 16 }}>
      <MendixMicroflowResourceTab onOpenMicroflow={resourceId => navigate(createMicroflowEditorPath(resourceId))} />
    </div>
  );
}
