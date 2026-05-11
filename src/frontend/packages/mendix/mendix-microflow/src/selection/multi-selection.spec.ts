import { describe, expect, it } from "vitest";

import { applyEditorGraphPatchToAuthoring, createObjectFromRegistry, createSequenceFlow, deleteObject, duplicateObjectSelection } from "../adapters";
import { sampleMicroflowSchema } from "../schema/sample";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema";
import { collectFlowsRecursive } from "../schema/utils/object-utils";

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
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("microflow multi selection", () => {
  it("keeps primary selection while storing multi selection arrays", () => {
    const a = createObjectFromRegistry(registry("activity:logMessage"), { x: 120, y: 120 });
    const b = createObjectFromRegistry(registry("activity:variableCreate"), { x: 260, y: 120 });
    const schema = applyEditorGraphPatchToAuthoring(schemaWith([a, b]), {
      selectedObjectId: a.id,
      selectedObjectIds: [a.id, b.id],
      selectedFlowIds: [],
      selectionMode: "multi",
    });

    expect(schema.editor.selection.objectId).toBe(a.id);
    expect(schema.editor.selection.objectIds).toEqual([a.id, b.id]);
    expect(schema.editor.selection.mode).toBe("multi");
  });

  it("filters deleted objects out of multi selection", () => {
    const a = createObjectFromRegistry(registry("activity:logMessage"), { x: 120, y: 120 });
    const b = createObjectFromRegistry(registry("activity:variableCreate"), { x: 260, y: 120 });
    const schema = applyEditorGraphPatchToAuthoring(schemaWith([a, b]), {
      selectedObjectId: a.id,
      selectedObjectIds: [a.id, b.id],
      selectionMode: "multi",
    });

    const next = deleteObject(schema, a.id);
    expect(next.editor.selection.objectId).toBeUndefined();
    expect(next.editor.selection.objectIds).toEqual([b.id]);
    expect(next.editor.selection.mode).toBe("single");
  });

  it("duplicates selected subgraph with internal flows", () => {
    const a = createObjectFromRegistry(registry("activity:logMessage"), { x: 120, y: 120 });
    const b = createObjectFromRegistry(registry("activity:variableCreate"), { x: 260, y: 120 });
    const flow = createSequenceFlow({ originObjectId: a.id, destinationObjectId: b.id });
    const schema = schemaWith([a, b], [flow]);

    const duplicated = duplicateObjectSelection(schema, { objectIds: [a.id, b.id] });
    expect(duplicated.objectCollection.objects).toHaveLength(4);
    expect(collectFlowsRecursive(duplicated)).toHaveLength(2);
    expect(duplicated.editor.selection.objectIds).toHaveLength(2);
    expect(duplicated.editor.selection.objectIds?.every(id => id !== a.id && id !== b.id)).toBe(true);
  });
});

