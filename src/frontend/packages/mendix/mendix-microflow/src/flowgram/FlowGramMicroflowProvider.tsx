import type { PropsWithChildren } from "react";

import { WorkflowRenderProvider } from "@coze-workflow/render";
import {
  createContainerNodePlugin,
  createFreeHistoryPlugin,
  createFreeLinesPlugin,
  createFreeSnapPlugin,
  createHistoryNodePlugin,
  type Plugin,
} from "@flowgram-adapter/free-layout-editor";

import { FlowGramMicroflowContainerModule } from "./FlowGramMicroflowPlugins";
import { FlowGramMicroflowLineRenderer } from "./FlowGramMicroflowLineRenderer";

function createMicroflowFlowGramPreset(): Plugin[] {
  return [
    createFreeLinesPlugin({
      renderInsideLine: FlowGramMicroflowLineRenderer,
    }),
    createFreeHistoryPlugin({
      enable: true,
      limit: 100,
    }),
    createFreeSnapPlugin({
      edgeColor: "#165dff",
      alignColor: "#165dff",
      edgeLineWidth: 1,
      alignLineWidth: 1,
      alignCrossWidth: 8,
    }),
    createHistoryNodePlugin({}),
    createContainerNodePlugin({}),
  ];
}

export function FlowGramMicroflowProvider({ children }: PropsWithChildren) {
  return (
    <WorkflowRenderProvider containerModules={[FlowGramMicroflowContainerModule]} preset={createMicroflowFlowGramPreset}>
      {children}
    </WorkflowRenderProvider>
  );
}
