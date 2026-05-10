import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../adapters/authoring-operations";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema/types";
import { createBusinessAutoLayoutPatch } from "./auto-layout-engine";

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
    id: "MF_LAYOUT",
    stableId: "MF_LAYOUT",
    name: "MF_Layout",
    displayName: "MF Layout",
    moduleId: "sales",
    moduleName: "Sales",
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

describe("createBusinessAutoLayoutPatch", () => {
  it("keeps branches parallel with readable row spacing in LR layout", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 80, y: 120 }, "start");
    const decision = createObjectFromRegistry(registry("decision"), { x: 240, y: 120 }, "decision");
    const trueNode = createObjectFromRegistry(registry("activity:variableChange"), { x: 420, y: 80 }, "true-node");
    const falseNode = createObjectFromRegistry(registry("activity:variableChange"), { x: 420, y: 180 }, "false-node");
    const schema = {
      ...schemaWith([start, decision, trueNode, falseNode]),
      flows: [
        createSequenceFlow({ id: "f-start-decision", originObjectId: "start", destinationObjectId: "decision" }),
        createSequenceFlow({ id: "f-decision-true", originObjectId: "decision", destinationObjectId: "true-node" }),
        createSequenceFlow({ id: "f-decision-false", originObjectId: "decision", destinationObjectId: "false-node" }),
      ],
    };

    const patch = createBusinessAutoLayoutPatch({ schema });
    const positionById = new Map((patch.movedNodes ?? []).map(node => [node.objectId, node.position]));

    expect(positionById.get("start")).toEqual({ x: 120, y: 120 });
    expect(positionById.get("decision")?.x).toBe(480);
    expect(positionById.get("true-node")?.x).toBe(840);
    expect(positionById.get("false-node")?.x).toBe(840);
    expect(Math.abs((positionById.get("false-node")?.y ?? 0) - (positionById.get("true-node")?.y ?? 0))).toBeGreaterThanOrEqual(210);
  });
});
