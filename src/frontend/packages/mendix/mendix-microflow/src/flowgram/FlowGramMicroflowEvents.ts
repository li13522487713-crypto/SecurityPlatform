import {
  FlowRendererContribution,
  FlowRendererKey,
  type FlowRendererRegistry,
  WorkflowDocumentOptions,
  type WorkflowPortEntity,
} from "@flowgram-adapter/free-layout-editor";

import { canConnectPorts } from "../node-registry";
import { toEditorGraph } from "../adapters";
import type { MicroflowSchema } from "../schema";
import { MICROFLOW_GRID_SIZE } from "./adapters/flowgram-coordinate";
import { FlowGramMicroflowNodeRenderer } from "./FlowGramMicroflowNodeRenderer";

export const FlowGramMicroflowBridgeServiceToken = Symbol.for(
  "atlas.mendix.microflow.FlowGramMicroflowBridgeService",
);

export class FlowGramMicroflowBridgeService {
  private schema?: MicroflowSchema;
  private gridEnabled = true;
  private gridSize = MICROFLOW_GRID_SIZE;

  setSchema(schema: MicroflowSchema) {
    this.schema = schema;
    this.gridEnabled = schema.editor.gridEnabled !== false;
  }

  getSchema() {
    return this.schema;
  }

  setGrid(enabled: boolean, gridSize = MICROFLOW_GRID_SIZE) {
    this.gridEnabled = enabled;
    this.gridSize = gridSize;
  }

  getGridConfig() {
    return {
      enabled: this.gridEnabled,
      size: this.gridSize,
    };
  }
}

function portId(port: WorkflowPortEntity): string | undefined {
  return typeof port.portID === "string" ? port.portID : port.portID === undefined ? undefined : String(port.portID);
}

export class FlowGramMicroflowDocumentOptions implements WorkflowDocumentOptions {
  constructor(private readonly bridge: FlowGramMicroflowBridgeService) {}

  canAddLine(fromPort: WorkflowPortEntity, toPort: WorkflowPortEntity): boolean {
    const schema = this.bridge.getSchema();
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

export class FlowGramMicroflowRenderContribution implements FlowRendererContribution {
  registerRenderer(renderer: FlowRendererRegistry): void {
    renderer.registerReactComponent(FlowRendererKey.NODE_RENDER, FlowGramMicroflowNodeRenderer);
  }
}
