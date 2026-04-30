import {
  FlowRendererContribution,
  FlowRendererKey,
  type FlowRendererRegistry,
  WorkflowDocumentOptions,
  type WorkflowPortEntity,
} from "@flowgram-adapter/free-layout-editor";

import { FlowGramMicroflowNodeRenderer } from "./FlowGramMicroflowNodeRenderer";

type FlowGramPortForBasicRules = WorkflowPortEntity & {
  disabled?: boolean;
  node?: {
    id?: string;
    parent?: { id?: string };
  };
};

export class FlowGramMicroflowDocumentOptions implements WorkflowDocumentOptions {
  canAddLine(fromPort: WorkflowPortEntity, toPort: WorkflowPortEntity): boolean {
    const source = fromPort as FlowGramPortForBasicRules;
    const target = toPort as FlowGramPortForBasicRules;
    if (source === target || source.node === target.node || source.disabled || target.disabled) {
      return false;
    }
    if (source.portType === "input" || target.portType !== "input") {
      return false;
    }
    return source.node?.parent?.id === target.node?.parent?.id;
  }
}

export class FlowGramMicroflowRenderContribution implements FlowRendererContribution {
  registerRenderer(renderer: FlowRendererRegistry): void {
    renderer.registerReactComponent(FlowRendererKey.NODE_RENDER, FlowGramMicroflowNodeRenderer);
  }
}
