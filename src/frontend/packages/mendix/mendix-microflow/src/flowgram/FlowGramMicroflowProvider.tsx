import type { PropsWithChildren } from "react";

import { WorkflowRenderProvider } from "@coze-workflow/render";

import { FlowGramMicroflowContainerModule } from "./FlowGramMicroflowPlugins";

export function FlowGramMicroflowProvider({ children }: PropsWithChildren) {
  return (
    <WorkflowRenderProvider containerModules={[FlowGramMicroflowContainerModule]}>
      {children}
    </WorkflowRenderProvider>
  );
}

