import { describe, expect, it } from "vitest";

import {
  createMicroflowFlowId,
  createMicroflowObjectId,
  createObjectFromRegistry,
  createSequenceFlow,
  deleteObject,
  duplicateObject,
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

  it("moves nodes by writing relativeMiddlePoint back to schema", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const moved = moveObject(schemaWith([start]), "start", { x: 240, y: 320 });

    expect(moved.objectCollection.objects.find(object => object.id === "start")?.relativeMiddlePoint).toEqual({ x: 240, y: 320 });
  });

  it("deletes nodes and clears related flows and selection", () => {
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

    expect(deleted.objectCollection.objects.map(object => object.id)).toEqual(["end"]);
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

  it("keeps A/B schema operations isolated", () => {
    const aStart = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "a-start");
    const bStart = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "b-start");
    const a = moveObject(schemaWith([aStart]), "a-start", { x: 160, y: 200 });
    const b = { ...schemaWith([bStart]), id: "MF_CANVAS_B", stableId: "MF_CANVAS_B" };

    expect(a.objectCollection.objects[0]?.relativeMiddlePoint).toEqual({ x: 160, y: 200 });
    expect(b.objectCollection.objects[0]?.relativeMiddlePoint).toEqual({ x: 80, y: 120 });
  });
});
