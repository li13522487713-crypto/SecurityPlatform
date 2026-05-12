import { describe, expect, it } from "vitest";

import { createObjectFromRegistry } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { sampleMicroflowSchema } from "../../schema/sample";
import type { MicroflowAuthoringSchema } from "../../schema";
import { applyEndEventDraft, buildEndEventDraft } from "./end-event-inline";

function registry(key: string) {
  const entry = defaultMicroflowNodeRegistry.find(item => getMicroflowNodeRegistryKey(item) === key);
  if (!entry) {
    throw new Error(`Missing registry entry ${key}`);
  }
  return entry;
}

describe("end-event-inline", () => {
  it("builds draft from returnValue.raw instead of subtitle fallback", () => {
    const draft = buildEndEventDraft({
      objectId: "end-1",
      objectKind: "endEvent",
      collectionId: "root-collection",
      title: "End",
      subtitle: "legacy subtitle should not win",
      officialType: "Microflows$EndEvent",
      disabled: false,
      validationState: "valid",
      issueCount: 0,
      returnValue: {
        raw: "if $approved then 'ok' else 'no'",
        inferredType: { kind: "string" },
        references: { variables: ["$approved"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
        diagnostics: [],
      },
    });

    expect(draft.returnExpression).toBe("if $approved then 'ok' else 'no'");
  });

  it("persists inline draft as end event returnValue.raw expression", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "inline-end");
    if (end.kind !== "endEvent") {
      throw new Error("Expected end event.");
    }
    const schema: MicroflowAuthoringSchema = {
      ...sampleMicroflowSchema,
      objectCollection: { ...sampleMicroflowSchema.objectCollection, objects: [end] },
      flows: [],
      returnType: { kind: "string" },
      editor: { ...sampleMicroflowSchema.editor, selection: {} },
    };

    const next = applyEndEventDraft(schema, end.id, {
      returnExpression: "if $approved then 'ok' else 'no'",
    });
    const updatedEnd = next.objectCollection.objects.find(object => object.id === end.id);

    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.raw : undefined).toBe("if $approved then 'ok' else 'no'");
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.inferredType : undefined).toEqual({ kind: "string" });
  });
});
