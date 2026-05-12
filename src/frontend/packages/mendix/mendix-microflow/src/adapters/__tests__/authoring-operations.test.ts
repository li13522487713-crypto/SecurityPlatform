import { describe, expect, it } from "vitest";

import {
  createMicroflowFlowId,
  createMicroflowObjectId,
  createObjectFromRegistry,
  createSequenceFlow,
  deleteObject,
  duplicateObject,
  duplicateObjectSelection,
  moveObject,
} from "../authoring-operations";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../../schema/types";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function schemaWith(objects: MicroflowObject[] = []): MicroflowSchema {
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: "MF_CANVAS_A",
    stableId: "MF_CANVAS_A",
    name: "MF_CanvasA",
    displayName: "MF Canvas A",
    moduleId: "procurement",
    moduleName: "Procurement",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "collection-root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects,
      flows: [],
    },
    flows: [],
    security: { applyEntityAccess: true, allowedModuleRoleIds: [] },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: true },
    validation: { issues: [] },
    editor: {
      viewport: { x: 0, y: 0, zoom: 1 },
      zoom: 1,
      selection: {},
    },
    audit: {
      version: "v1",
      status: "draft",
    },
  };
}

describe("authoring schema canvas operations", () => {
  it("generates unique object and flow ids within the current schema", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start-fixed");
    const flow = createSequenceFlow({ id: "flow-fixed", originObjectId: "start-fixed", destinationObjectId: "end-fixed" });
    const schema = { ...schemaWith([start]), flows: [flow] };

    expect(createMicroflowObjectId(schema, "start-fixed")).not.toBe("start-fixed");
    expect(createMicroflowFlowId(schema, "flow-fixed")).not.toBe("flow-fixed");
  });

  it("creates tryCatch and errorHandler nodes as structural objects instead of action fallbacks", () => {
    const tryCatch = createObjectFromRegistry(registry("tryCatch"), { x: 160, y: 120 }, "try-catch");
    const errorHandler = createObjectFromRegistry(registry("errorHandler"), { x: 280, y: 120 }, "error-handler");

    expect(tryCatch).toMatchObject({
      id: "try-catch",
      kind: "tryCatch",
      officialType: "Microflows$TryCatch",
      tryBranchKey: "try",
      catchBranchKey: "catch",
      finallyBranchKey: "finally",
      errorVariableName: "latestError",
    });
    expect(errorHandler).toMatchObject({
      id: "error-handler",
      kind: "errorHandler",
      officialType: "Microflows$ErrorHandler",
      policy: "rollback",
      continueOnError: false,
    });
  });

  it("moves non-start nodes by writing relativeMiddlePoint back to schema", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 320, y: 120 }, "end");
    const moved = moveObject(schemaWith([end]), "end", { x: 240, y: 320 });

    expect(moved.objectCollection.objects.find(object => object.id === "end")?.relativeMiddlePoint).toEqual({ x: 240, y: 320 });
  });

  it("does not move StartEvent", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const moved = moveObject(schemaWith([start]), "start", { x: 240, y: 320 });

    expect(moved.objectCollection.objects.find(object => object.id === "start")?.relativeMiddlePoint).toEqual({ x: 80, y: 120 });
  });

  it("does not delete StartEvent", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const end = createObjectFromRegistry(registry("endEvent"), { x: 320, y: 120 }, "end");
    const flow = createSequenceFlow({ id: "flow-start-end", originObjectId: "start", destinationObjectId: "end" });
    const schema = {
      ...schemaWith([start, end]),
      flows: [flow],
      editor: {
        ...schemaWith().editor,
        selection: { objectId: "start", flowId: "flow-start-end", collectionId: "collection-root" },
        selectedObjectId: "start",
        selectedFlowId: "flow-start-end",
        selectedCollectionId: "collection-root",
      },
    };

    const deleted = deleteObject(schema, "start");

    expect(deleted.objectCollection.objects.map(object => object.id)).toEqual(["start", "end"]);
    expect(deleted.flows.map(item => item.id)).toEqual(["flow-start-end"]);
    expect(deleted.editor.selection.objectId).toBe("start");
    expect(deleted.editor.selection.flowId).toBe("flow-start-end");
  });

  it("deletes non-start nodes and clears related flows and selection", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const end = createObjectFromRegistry(registry("endEvent"), { x: 320, y: 120 }, "end");
    const flow = createSequenceFlow({ id: "flow-start-end", originObjectId: "start", destinationObjectId: "end" });
    const schema = {
      ...schemaWith([start, end]),
      flows: [flow],
      editor: {
        ...schemaWith().editor,
        selection: { objectId: "end", flowId: "flow-start-end", collectionId: "collection-root" },
        selectedObjectId: "end",
        selectedFlowId: "flow-start-end",
        selectedCollectionId: "collection-root",
      },
    };

    const deleted = deleteObject(schema, "end");

    expect(deleted.objectCollection.objects.map(object => object.id)).toEqual(["start"]);
    expect(deleted.flows).toEqual([]);
    expect(deleted.editor.selection.objectId).toBeUndefined();
    expect(deleted.editor.selection.flowId).toBeUndefined();
  });

  it("duplicates a node with a new id, offset position, copied config, and no copied flows", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 320, y: 120 }, "end");
    const schema = schemaWith([end]);
    const duplicated = duplicateObject(schema, "end");
    const objects = duplicated.objectCollection.objects;
    const copy = objects.find(object => object.id !== "end");

    expect(objects).toHaveLength(2);
    expect(copy?.id).toBeDefined();
    expect(copy?.id).not.toBe("end");
    expect(copy?.relativeMiddlePoint).toEqual({ x: 400, y: 180 });
    expect(copy?.caption).toBe("End Event Copy");
    expect(duplicated.flows).toEqual([]);
    expect(duplicated.editor.selection.objectId).toBe(copy?.id);
  });

  it("does not duplicate StartEvent from multi-selection", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const end = createObjectFromRegistry(registry("endEvent"), { x: 320, y: 120 }, "end");
    const flow = createSequenceFlow({ id: "flow-start-end", originObjectId: "start", destinationObjectId: "end" });
    const schema = {
      ...schemaWith([start, end]),
      flows: [flow],
    };

    const duplicated = duplicateObjectSelection(schema, {
      objectIds: ["start", "end"],
      flowIds: ["flow-start-end"],
    });

    const startEvents = duplicated.objectCollection.objects.filter(object => object.kind === "startEvent");
    const endEvents = duplicated.objectCollection.objects.filter(object => object.kind === "endEvent");

    expect(startEvents).toHaveLength(1);
    expect(endEvents).toHaveLength(2);
    expect(duplicated.flows).toHaveLength(1);
    expect(duplicated.editor.selection.objectIds).toHaveLength(1);
    expect(duplicated.editor.selection.objectId).toBe(endEvents.find(object => object.id !== "end")?.id);
  });

  it("keeps A/B schema operations isolated", () => {
    const aStart = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "a-start");
    const bStart = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "b-start");
    const a = moveObject(schemaWith([aStart]), "a-start", { x: 160, y: 200 });
    const b = { ...schemaWith([bStart]), id: "MF_CANVAS_B", stableId: "MF_CANVAS_B" };

    expect(a.objectCollection.objects[0]?.relativeMiddlePoint).toEqual({ x: 80, y: 120 });
    expect(b.objectCollection.objects[0]?.relativeMiddlePoint).toEqual({ x: 80, y: 120 });
  });
});
