import { WorkflowPortRender } from "@coze-workflow/render";
import type { WorkflowPortEntity } from "@flowgram-adapter/free-layout-editor";

export function FlowGramMicroflowPortRenderer({ port }: { port: WorkflowPortEntity }) {
  return <WorkflowPortRender entity={port} />;
}

