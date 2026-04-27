import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../adapters";
import { getMicroflowNodeRegistryKey, defaultMicroflowNodeRegistry } from "../node-registry";
import { sampleMicroflowSchema, type MicroflowObject, type MicroflowSchema } from "../schema";
import {
  updateActionConfig,
  updateFlow,
  updateFlowLabel,
  updateObject,
  updateObjectCaption,
  updateObjectDescription,
  updateParameter,
} from "./utils";

function registry(key: string) {
  const entry = defaultMicroflowNodeRegistry.find(item => getMicroflowNodeRegistryKey(item) === key);
  if (!entry) {
    throw new Error(`Missing registry entry ${key}`);
  }
  return entry;
}

function schemaWith(objects: MicroflowObject[], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("property panel schema-bound update helpers", () => {
  it("updates object caption and documentation immutably", () => {
    const start = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "property-start");
    const schema = schemaWith([start]);
    const renamed = updateObject(schema, start.id, object => updateObjectCaption(updateObjectDescription(object, "Starts validation"), "Start_Validate"));

    expect(renamed.objectCollection.objects[0]?.caption).toBe("Start_Validate");
    expect(renamed.objectCollection.objects[0]?.documentation).toBe("Starts validation");
    expect(schema.objectCollection.objects[0]?.caption).not.toBe("Start_Validate");
  });

  it("updates action config without injecting demo metadata", () => {
    const activity = createObjectFromRegistry(registry("activity:restCall"), { x: 0, y: 0 }, "property-rest");
    if (activity.kind !== "actionActivity" || activity.action.kind !== "restCall") {
      throw new Error("Expected restCall action activity.");
    }
    const updated = updateActionConfig(activity, "restCall", {
      request: {
        ...activity.action.request,
        method: "POST",
        urlExpression: { ...activity.action.request.urlExpression, raw: "/api/test" },
      },
    });

    expect(updated.action.kind).toBe("restCall");
    if (updated.action.kind !== "restCall") {
      throw new Error("Expected updated restCall action.");
    }
    expect(updated.action.request.method).toBe("POST");
    expect(updated.action.request.urlExpression.raw).toBe("/api/test");
    expect(JSON.stringify(updated)).not.toMatch(/Sales\.|MF_ValidateOrder|api\.example\.com/i);
  });

  it("updates parameter config while keeping object schema isolated", () => {
    const parameterObject = createObjectFromRegistry(registry("parameter"), { x: 0, y: 0 }, "property-param");
    if (parameterObject.kind !== "parameterObject") {
      throw new Error("Expected parameter object.");
    }
    const schema = {
      ...schemaWith([parameterObject]),
      parameters: [{
        id: parameterObject.parameterId,
        name: "",
        dataType: { kind: "string" as const },
        required: false,
      }],
    };
    const updated = updateParameter(schema, parameterObject.parameterId, {
      name: "amount",
      dataType: { kind: "decimal" },
      required: true,
    });

    expect(updated.parameters[0]).toMatchObject({ name: "amount", dataType: { kind: "decimal" }, required: true });
    expect(schema.parameters[0]?.name).toBe("");
  });

  it("updates flow label and nested loop flow cases", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 0, y: 0 }, "property-loop");
    const first = createObjectFromRegistry(registry("activity:createVariable"), { x: 40, y: 40 }, "loop-first");
    const second = createObjectFromRegistry(registry("activity:changeVariable"), { x: 240, y: 40 }, "loop-second");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loop object.");
    }
    const flow = createSequenceFlow({ originObjectId: first.id, destinationObjectId: second.id });
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, objects: [first, second], flows: [flow] } }]);
    const updated = updateFlow(schema, flow.id, item => updateFlowLabel(item, "Inside loop"));
    const nestedLoop = updated.objectCollection.objects[0];

    expect(nestedLoop?.kind).toBe("loopedActivity");
    expect(nestedLoop?.kind === "loopedActivity" ? nestedLoop.objectCollection.flows?.[0]?.editor.label : undefined).toBe("Inside loop");
    expect(schema.objectCollection.objects[0]?.kind === "loopedActivity" ? schema.objectCollection.objects[0].objectCollection.flows?.[0]?.editor.label : undefined).toBeUndefined();
  });

  it("keeps A/B property edits isolated", () => {
    const aStart = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "property-a-start");
    const bStart = createObjectFromRegistry(registry("startEvent"), { x: 0, y: 0 }, "property-b-start");
    const schemaA = schemaWith([aStart]);
    const schemaB = schemaWith([bStart]);
    const updatedA = updateObject(schemaA, aStart.id, object => updateObjectCaption(object, "A_Start"));

    expect(updatedA.objectCollection.objects[0]?.caption).toBe("A_Start");
    expect(schemaB.objectCollection.objects[0]?.caption).not.toBe("A_Start");
  });
});
