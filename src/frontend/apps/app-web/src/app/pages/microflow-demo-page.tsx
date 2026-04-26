import { useMemo } from "react";
import {
  createLocalMicroflowApiClient,
  MicroflowEditor,
  sampleMicroflowSchema
} from "@atlas/microflow";

export function MicroflowDemoPage() {
  const apiClient = useMemo(() => createLocalMicroflowApiClient([sampleMicroflowSchema]), []);

  return (
    <div style={{ height: "calc(100vh - 60px)", minHeight: 720 }}>
      <MicroflowEditor schema={sampleMicroflowSchema} apiClient={apiClient} />
    </div>
  );
}
