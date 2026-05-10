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
    const sourceNodeId = source.node?.id;
    const targetNodeId = target.node?.id;
    const sourceParentId = source.node?.parent?.id;
    const targetParentId = target.node?.parent?.id;
    if (
      source.disabled
      || target.disabled
      || !sourceNodeId
      || !targetNodeId
      || sourceNodeId === targetNodeId
      || source.portType === "input"
      || target.portType !== "input"
    ) {
      return false;
    }

    // 允许容器节点（如 loop）直接连到其 body 子节点。
    if (targetParentId === sourceNodeId) {
      return true;
    }

    // 块内节点之间允许同父域连接。
    if (sourceParentId && targetParentId && sourceParentId === targetParentId) {
      return true;
    }

    // 同层节点可连接，不允许跨层/跨容器连线。
    if (!sourceParentId && !targetParentId) {
      return true;
    }

    return false;
  }
}

export class FlowGramMicroflowRenderContribution implements FlowRendererContribution {
  registerRenderer(renderer: FlowRendererRegistry): void {
    renderer.registerReactComponent(FlowRendererKey.NODE_RENDER, FlowGramMicroflowNodeRenderer);
  }
}
