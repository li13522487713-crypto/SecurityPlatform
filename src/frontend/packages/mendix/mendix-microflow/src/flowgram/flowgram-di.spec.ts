import { describe, expect, it, vi } from "vitest";
import { Container } from "inversify";
import { WorkflowDocumentOptions, type WorkflowPortEntity } from "@flowgram-adapter/free-layout-editor";

vi.mock("./FlowGramMicroflowNodeRenderer", () => ({
  FlowGramMicroflowNodeRenderer: () => null,
}));

import { createObjectFromRegistry, toEditorGraph } from "../adapters";
import {
  defaultMicroflowNodeRegistry,
  getMicroflowNodeRegistryKey,
} from "../node-registry";
import { sampleMicroflowSchema, type MicroflowObject, type MicroflowSchema } from "../schema";
import {
  FlowGramMicroflowDocumentOptions,
  FlowGramMicroflowSchemaContextService,
  FlowGramMicroflowSchemaContextServiceToken,
} from "./FlowGramMicroflowEvents";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function schemaWith(objects: MicroflowObject[]): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("FlowGram microflow DI", () => {
  it("binds document options to a stable schema context singleton", () => {
    const container = new Container({ defaultScope: "Singleton" });
    container
      .bind<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken)
      .to(FlowGramMicroflowSchemaContextService)
      .inSingletonScope();
    container
      .bind(FlowGramMicroflowDocumentOptions)
      .toDynamicValue(ctx => new FlowGramMicroflowDocumentOptions(
        ctx.container.get<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken),
      ))
      .inSingletonScope();
    container.bind(WorkflowDocumentOptions).toService(FlowGramMicroflowDocumentOptions);
    const schemaContext = container.get<FlowGramMicroflowSchemaContextService>(FlowGramMicroflowSchemaContextServiceToken);
    const options = container.get<FlowGramMicroflowDocumentOptions>(FlowGramMicroflowDocumentOptions);
    const workflowOptions = container.get<WorkflowDocumentOptions>(WorkflowDocumentOptions);

    expect(options).toBe(workflowOptions);
    const start = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "di-start");
    const end = createObjectFromRegistry(registry("endEvent"), { x: 240, y: 0 }, "di-end");
    const schema = schemaWith([start, end]);
    const graph = toEditorGraph(schema);
    const sourcePort = graph.nodes.find(node => node.objectId === start.id)?.ports.find(port => port.direction === "output");
    const targetPort = graph.nodes.find(node => node.objectId === end.id)?.ports.find(port => port.direction === "input");
    if (!sourcePort || !targetPort) {
      throw new Error("Expected start and end ports.");
    }

    schemaContext.setSchema(schema);
    expect(schemaContext.getSchema()).toBe(schema);
    expect(options.canAddLine({ portID: sourcePort.id } as WorkflowPortEntity, { portID: targetPort.id } as WorkflowPortEntity)).toBe(true);
  });
});
