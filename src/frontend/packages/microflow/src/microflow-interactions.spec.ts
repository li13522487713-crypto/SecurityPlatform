import { describe, expect, it } from "vitest";
import { createObjectFromRegistry, createSequenceFlow, toEditorGraph } from "./adapters";
import { canConnectPorts, defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "./node-registry";
import { sampleMicroflowSchema, validateMicroflowSchema } from "./schema";
import { buildVariableIndex } from "./variables";
import { authoringToFlowGram } from "./flowgram/adapters/authoring-to-flowgram";
import { createMicroflowFlowFromPorts } from "./flowgram/adapters/flowgram-edge-factory";
import type { MicroflowObject, MicroflowSchema } from "./schema";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function schemaWith(objects: MicroflowObject[], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} }
  };
}

describe("microflow editor interactions", () => {
  it("creates concrete objects from registry entries", () => {
    expect(createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }).kind).toBe("startEvent");
    expect(createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 100, y: 0 }).kind).toBe("actionActivity");
    expect(createObjectFromRegistry(registry("activity:callRest"), { x: 200, y: 0 }).kind).toBe("actionActivity");
    expect(createObjectFromRegistry(registry("annotation"), { x: 300, y: 0 }).kind).toBe("annotation");
    expect(createObjectFromRegistry(registry("parameter"), { x: 400, y: 0 }).kind).toBe("parameterObject");
  });

  it("allows normal sequence flow from start to retrieve", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "start-test");
    const retrieve = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 200, y: 0 }, "retrieve-test");
    const schema = schemaWith([start, retrieve]);
    const graph = toEditorGraph(schema);
    const source = graph.nodes.find(node => node.objectId === start.id)?.ports.find(port => port.direction === "output");
    const target = graph.nodes.find(node => node.objectId === retrieve.id)?.ports.find(port => port.direction === "input");
    expect(source && target ? canConnectPorts(schema, source, target).allowed : false).toBe(true);
  });

  it("rejects terminal source and normal flow into error event", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "end-test");
    const error = createObjectFromRegistry(registry("errorEvent"), { x: 200, y: 0 }, "error-test");
    const retrieve = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 400, y: 0 }, "retrieve-test");
    const schema = schemaWith([end, error, retrieve]);
    const graph = toEditorGraph(schema);
    const endOut = graph.nodes.find(node => node.objectId === end.id)?.ports.find(port => port.direction === "output");
    expect(endOut).toBeUndefined();
    const retrieveOut = graph.nodes.find(node => node.objectId === retrieve.id)?.ports.find(port => port.kind === "sequenceOut");
    const errorIn = graph.nodes.find(node => node.objectId === error.id)?.ports.find(port => port.direction === "input");
    expect(retrieveOut && errorIn ? canConnectPorts(schema, retrieveOut, errorIn).allowed : true).toBe(false);
  });

  it("detects duplicate decision cases", () => {
    const decision = createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, "decision-test");
    const first = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: -80 }, "change-1");
    const second = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: 80 }, "change-2");
    const flow = createSequenceFlow({
      originObjectId: decision.id,
      destinationObjectId: first.id,
      originConnectionIndex: 1,
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }]
    });
    const schema = schemaWith([decision, first, second], [flow]);
    const graph = toEditorGraph(schema);
    const truePort = graph.nodes.find(node => node.objectId === decision.id)?.ports.find(port => port.label === "True");
    const target = graph.nodes.find(node => node.objectId === second.id)?.ports.find(port => port.direction === "input");
    expect(truePort && target ? canConnectPorts(schema, truePort, target).allowed : true).toBe(false);
  });

  it("derives FlowGram JSON from AuthoringSchema without losing flow semantics", () => {
    const json = authoringToFlowGram(sampleMicroflowSchema, validateMicroflowSchema(sampleMicroflowSchema), []);
    expect(json.nodes.length).toBeGreaterThan(0);
    expect(json.edges.length).toBeGreaterThan(0);
    expect(json.nodes[0]?.data).toHaveProperty("objectId");
    expect(json.edges[0]?.data).toHaveProperty("flowId");
  });

  it("creates a boolean decision condition flow from FlowGram ports", () => {
    const decision = createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, "decision-flowgram");
    const change = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: 0 }, "change-flowgram");
    const schema = schemaWith([decision, change]);
    const graph = toEditorGraph(schema);
    const source = graph.nodes.find(node => node.objectId === decision.id)?.ports.find(port => port.kind === "decisionOut");
    const target = graph.nodes.find(node => node.objectId === change.id)?.ports.find(port => port.direction === "input");
    if (!source || !target) {
      throw new Error("Expected decision and target ports.");
    }
    const flow = createMicroflowFlowFromPorts(schema, source, target, {
      caseValues: [{ kind: "boolean", value: false, persistedValue: "false" }],
      label: "否",
    });
    expect(flow.kind).toBe("sequence");
    expect(flow.editor.edgeKind).toBe("decisionCondition");
    expect(flow.editor.label).toBe("否");
    expect(flow.caseValues[0]).toMatchObject({ kind: "boolean", value: false, persistedValue: "false" });
  });

  it("builds variable index and validates scoped expression errors", () => {
    const index = buildVariableIndex(sampleMicroflowSchema);
    expect(index.parameters.orderId.name).toBe("orderId");
    expect(index.objectOutputs.order.name).toBe("order");
    expect(index.systemVariables.$currentIndex.name).toBe("$currentIndex");
    const issues = validateMicroflowSchema({
      ...sampleMicroflowSchema,
      objectCollection: {
        ...sampleMicroflowSchema.objectCollection,
        objects: sampleMicroflowSchema.objectCollection.objects.map(object => object.kind === "endEvent"
          ? { ...object, returnValue: { raw: "$currentIndex", references: { variables: ["$currentIndex"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] } } }
          : object)
      }
    });
    expect(issues.some(issue => issue.code === "MF_EXPRESSION_INVALID" || issue.code === "MF_EXPRESSION_UNKNOWN_VARIABLE")).toBe(true);
  });
});
