import { describe, expect, it } from "vitest";
import { applyEditorGraphPatchToAuthoring, createAutoLayoutPatch, createObjectFromRegistry, createSequenceFlow, deleteObject, toEditorGraph } from "./adapters";
import { canConnectPorts, defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "./node-registry";
import { sampleMicroflowSchema, validateMicroflowSchema } from "./schema";
import { buildVariableIndex } from "./variables";
import { authoringToFlowGram } from "./flowgram/adapters/authoring-to-flowgram";
import { createMicroflowFlowFromPorts } from "./flowgram/adapters/flowgram-edge-factory";
import { flowGramPositionPatch, flowGramSelectionPatch } from "./flowgram/adapters/flowgram-to-authoring-patch";
import {
  enumerationCaseValue,
  fallbackCaseValue,
  getEnumerationCaseOptions,
  getObjectTypeCaseOptions,
  inheritanceCaseValue,
} from "./flowgram/adapters/flowgram-case-options";
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
    const actionNode = json.nodes.find(node => (node.data as { actionKind?: string }).actionKind);
    expect(actionNode?.data).toHaveProperty("action");
    const semanticEdge = json.edges.find(edge => {
      const data = edge.data as { edgeKind?: string; caseValues?: unknown[]; isErrorHandler?: boolean };
      return data.edgeKind === "decisionCondition" || data.edgeKind === "objectTypeCondition" || data.isErrorHandler;
    });
    if (semanticEdge) {
      expect(semanticEdge.data).toHaveProperty("caseValues");
      expect(semanticEdge.data).toHaveProperty("isErrorHandler");
    }
  });

  it("creates AuthoringSchema patches from FlowGram move, resize, and selection changes", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "flowgram-patch-start");
    const schema = schemaWith([start]);
    const json = authoringToFlowGram(schema, [], []);
    const node = json.nodes.find(item => item.id === start.id);
    if (!node) {
      throw new Error("Expected FlowGram node.");
    }
    node.meta = {
      ...node.meta,
      position: { x: 96, y: 64 },
      size: { width: 180, height: 80 },
    };
    const patch = flowGramPositionPatch(schema, json);
    expect(patch.movedNodes).toEqual([{ objectId: start.id, position: { x: 96, y: 64 } }]);
    expect(patch.resizedNodes).toEqual([{ objectId: start.id, size: { width: 180, height: 80 } }]);
    const next = applyEditorGraphPatchToAuthoring(schema, patch);
    expect(next.objectCollection.objects[0]?.relativeMiddlePoint).toEqual({ x: 96, y: 64 });
    expect(next.objectCollection.objects[0]?.size).toEqual({ width: 180, height: 80 });
    expect(flowGramSelectionPatch({ objectId: start.id })).toEqual({ selectedObjectId: start.id, selectedFlowId: undefined });
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

  it("creates an auto layout patch from root flow order", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 500, y: 500 }, "layout-start");
    const retrieve = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 100, y: 500 }, "layout-retrieve");
    const end = createObjectFromRegistry(registry("endEvent"), { x: 100, y: 100 }, "layout-end");
    const first = createSequenceFlow({ originObjectId: start.id, destinationObjectId: retrieve.id, originConnectionIndex: 0 });
    const second = createSequenceFlow({ originObjectId: retrieve.id, destinationObjectId: end.id, originConnectionIndex: 0 });
    const patch = createAutoLayoutPatch(schemaWith([end, retrieve, start], [first, second]));
    const positions = new Map(patch.movedNodes?.map(item => [item.objectId, item.position]));
    expect(positions.get(start.id)?.x).toBeLessThan(positions.get(retrieve.id)?.x ?? 0);
    expect(positions.get(retrieve.id)?.x).toBeLessThan(positions.get(end.id)?.x ?? 0);
  });

  it("blocks direct flows across loop objectCollection boundaries", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "boundary-loop");
    const rootAction = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 0, y: 120 }, "boundary-root");
    const loopAction = createObjectFromRegistry(registry("activity:objectChange"), { x: 220, y: 180 }, "boundary-child");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, objects: [loopAction] } }, rootAction]);
    const graph = toEditorGraph(schema);
    const source = graph.nodes.find(node => node.objectId === rootAction.id)?.ports.find(port => port.kind === "sequenceOut");
    const target = graph.nodes.find(node => node.objectId === loopAction.id)?.ports.find(port => port.direction === "input");
    expect(source && target ? canConnectPorts(schema, source, target).allowed : true).toBe(false);

    const invalidFlow = createSequenceFlow({ originObjectId: rootAction.id, destinationObjectId: loopAction.id });
    const issues = validateMicroflowSchema({ ...schema, flows: [invalidFlow] });
    expect(issues.some(issue => issue.code === "MF_FLOW_LOOP_BOUNDARY")).toBe(true);
  });

  it("adds loop internal objects to the loop collection and deletes descendant flows with the loop", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "delete-loop");
    const rootAction = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 0, y: 120 }, "delete-root");
    const loopAction = createObjectFromRegistry(registry("activity:objectChange"), { x: 220, y: 180 }, "delete-child");
    const baseSchema = schemaWith([loop, rootAction]);
    const withChild = applyEditorGraphPatchToAuthoring(baseSchema, {
      addObject: { object: loopAction, parentLoopObjectId: loop.id },
      selectedObjectId: loopAction.id,
    });
    const updatedLoop = withChild.objectCollection.objects.find(object => object.id === loop.id);
    expect(updatedLoop?.kind === "loopedActivity" ? updatedLoop.objectCollection.objects.some(object => object.id === loopAction.id) : false).toBe(true);
    expect(withChild.objectCollection.objects.some(object => object.id === loopAction.id)).toBe(false);

    const danglingFlow = createSequenceFlow({ originObjectId: loopAction.id, destinationObjectId: rootAction.id });
    const deleted = deleteObject({ ...withChild, flows: [danglingFlow] }, loop.id);
    expect(deleted.flows.some(flow => flow.originObjectId === loopAction.id || flow.destinationObjectId === loopAction.id)).toBe(false);
    expect(deleted.editor.selection.objectId).toBeUndefined();
  });

  it("projects loop body summary to FlowGram and resizes loop containers during auto layout", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "summary-loop");
    const loopAction = createObjectFromRegistry(registry("activity:objectChange"), { x: 220, y: 180 }, "summary-child");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const flow = createSequenceFlow({ originObjectId: loopAction.id, destinationObjectId: loopAction.id });
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, objects: [loopAction] } }], [flow]);
    const json = authoringToFlowGram(schema, validateMicroflowSchema(schema), []);
    const loopNode = json.nodes.find(node => node.id === loop.id);
    expect(loopNode?.data).toMatchObject({
      objectId: loop.id,
      loopSummary: {
        childCount: 1,
        actionCount: 1,
        flowCount: 1,
      },
    });

    const patch = createAutoLayoutPatch(schema);
    expect(patch.resizedNodes?.some(item => item.objectId === loop.id && item.size.height > loop.size.height)).toBe(true);
  });

  it("creates sequence flows between Loop internal objects without crossing collection boundaries", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "internal-flow-loop");
    const first = createObjectFromRegistry(registry("activity:objectChange"), { x: 180, y: 180 }, "internal-first");
    const second = createObjectFromRegistry(registry("activity:logMessage"), { x: 420, y: 180 }, "internal-second");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, objects: [first, second] } }]);
    const graph = toEditorGraph(schema);
    const source = graph.nodes.find(node => node.objectId === first.id)?.ports.find(port => port.kind === "sequenceOut");
    const target = graph.nodes.find(node => node.objectId === second.id)?.ports.find(port => port.direction === "input");
    if (!source || !target) {
      throw new Error("Expected loop internal ports.");
    }
    expect(canConnectPorts(schema, source, target).allowed).toBe(true);
    const flow = createMicroflowFlowFromPorts(schema, source, target);
    const next = applyEditorGraphPatchToAuthoring(schema, {
      addFlow: flow,
      selectedFlowId: flow.id,
      selectedObjectId: undefined,
    });
    expect(next.flows.some(item => item.id === flow.id && item.originObjectId === first.id && item.destinationObjectId === second.id)).toBe(true);
    expect(next.editor.selection.flowId).toBe(flow.id);
    expect(validateMicroflowSchema(next).some(issue => issue.code === "MF_FLOW_LOOP_BOUNDARY")).toBe(false);
  });

  it("offers enumeration cases and validates duplicate enum values", () => {
    const decision = {
      ...createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, "enum-decision"),
      splitCondition: {
        kind: "expression" as const,
        expression: { raw: "$Order/Status", references: { variables: ["Order"], entities: [], attributes: ["Status"], associations: [], enumerations: [], functions: [] }, diagnostics: [], inferredType: { kind: "enumeration" as const, enumerationQualifiedName: "Sales.OrderStatus" } },
        resultType: "enumeration" as const,
        enumerationQualifiedName: "Sales.OrderStatus",
      },
    };
    const first = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: -80 }, "enum-change-1");
    const second = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: 80 }, "enum-change-2");
    const flow = createSequenceFlow({
      originObjectId: decision.id,
      destinationObjectId: first.id,
      originConnectionIndex: 1,
      edgeKind: "decisionCondition",
      caseValues: [enumerationCaseValue("Sales.OrderStatus", "Pending")]
    });
    const schema = schemaWith([decision, first, second], [flow]);
    const options = getEnumerationCaseOptions(schema, decision.id);
    expect(options.find(option => option.caseValue.kind === "enumeration" && option.caseValue.value === "Pending")?.disabled).toBe(true);
    expect(options.find(option => option.caseValue.kind === "enumeration" && option.caseValue.value === "Completed")?.disabled).toBe(false);
    const duplicateIssues = validateMicroflowSchema({
      ...schema,
      flows: [
        flow,
        createSequenceFlow({
          originObjectId: decision.id,
          destinationObjectId: second.id,
          originConnectionIndex: 1,
          edgeKind: "decisionCondition",
          caseValues: [enumerationCaseValue("Sales.OrderStatus", "Pending")]
        })
      ]
    });
    expect(duplicateIssues.some(issue => issue.code === "MF_DECISION_CASE_DUPLICATED")).toBe(true);
  });

  it("offers object type specialization, empty, and fallback cases", () => {
    const split = {
      ...createObjectFromRegistry(registry("objectTypeDecision"), { x: 0, y: 0 }, "type-decision"),
      inputObjectVariableName: "payment",
      entity: { generalizedEntityQualifiedName: "Sales.PaymentMethod", allowedSpecializations: ["Sales.CardPayment", "Sales.BankTransferPayment"] },
    };
    const first = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: -80 }, "type-change-1");
    const second = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: 80 }, "type-change-2");
    const flow = createSequenceFlow({
      originObjectId: split.id,
      destinationObjectId: first.id,
      originConnectionIndex: 1,
      edgeKind: "objectTypeCondition",
      caseValues: [inheritanceCaseValue("Sales.CardPayment")]
    });
    const schema = schemaWith([split, first, second], [flow]);
    const options = getObjectTypeCaseOptions(schema, split.id);
    expect(options.find(option => option.caseValue.kind === "inheritance" && option.caseValue.entityQualifiedName === "Sales.CardPayment")?.disabled).toBe(true);
    expect(options.some(option => option.caseValue.kind === "empty")).toBe(true);
    expect(options.some(option => option.caseValue.kind === "fallback")).toBe(true);
    const duplicateIssues = validateMicroflowSchema({
      ...schema,
      flows: [
        flow,
        createSequenceFlow({
          originObjectId: split.id,
          destinationObjectId: second.id,
          originConnectionIndex: 1,
          edgeKind: "objectTypeCondition",
          caseValues: [fallbackCaseValue(), fallbackCaseValue()]
        })
      ]
    });
    expect(duplicateIssues.some(issue => issue.code === "MF_OBJECT_TYPE_CASE_DUPLICATED" || issue.code === "MF_DECISION_DUPLICATE_CASE")).toBe(true);
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
