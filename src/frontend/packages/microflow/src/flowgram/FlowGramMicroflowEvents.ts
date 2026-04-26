import { inject, injectable } from "inversify";
import {
  FlowDocumentContribution,
  FlowRendererContribution,
  FlowRendererKey,
  type FlowRendererRegistry,
  WorkflowDocumentOptions,
  type WorkflowPortEntity,
} from "@flowgram-adapter/free-layout-editor";

import { canConnectPorts } from "../node-registry";
import { toEditorGraph } from "../adapters";
import type { MicroflowSchema } from "../schema";
import { FlowGramMicroflowNodeRenderer } from "./FlowGramMicroflowNodeRenderer";

export class FlowGramMicroflowBridgeService {
  schema?: MicroflowSchema;

  setSchema(schema: MicroflowSchema) {
    this.schema = schema;
  }
}

function portId(port: WorkflowPortEntity): string | undefined {
  return port.portID;
}

@injectable()
export class FlowGramMicroflowDocumentOptions implements WorkflowDocumentOptions {
  constructor(@inject(FlowGramMicroflowBridgeService) private readonly bridge: FlowGramMicroflowBridgeService) {}

  canAddLine(fromPort: WorkflowPortEntity, toPort: WorkflowPortEntity): boolean {
    const schema = this.bridge.schema;
    if (!schema) {
      return false;
    }
    const ports = toEditorGraph(schema).nodes.flatMap(node => node.ports);
    const sourcePort = ports.find(port => port.id === portId(fromPort));
    const targetPort = ports.find(port => port.id === portId(toPort));
    if (!sourcePort || !targetPort) {
      return false;
    }
    return canConnectPorts(schema, sourcePort, targetPort).allowed;
  }
}

@injectable()
export class FlowGramMicroflowRenderContribution implements FlowRendererContribution, FlowDocumentContribution {
  registerRenderer(renderer: FlowRendererRegistry): void {
    renderer.registerReactComponent(FlowRendererKey.NODE_RENDER, FlowGramMicroflowNodeRenderer);
  }
}
