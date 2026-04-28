import { describe, expect, it } from "vitest";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import {
  canDeleteMicroflowFromReferences,
  parseMicroflowCallees,
  resolveReferenceDisplayName
} from "../../references/microflow-reference-utils";
import type { MicroflowReference } from "../../references/microflow-reference-types";
import type { StudioMicroflowDefinitionView } from "../studio-microflow-types";

function schemaWithAction(action: Record<string, unknown>): MicroflowAuthoringSchema {
  return {
    id: "source-mf",
    name: "MF_Source",
    displayName: "MF Source",
    moduleId: "procurement",
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        {
          id: "node-call",
          stableId: "stable-node-call",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          caption: "Call target",
          autoGenerateCaption: false,
          backgroundColor: "blue",
          disabled: false,
          relativeMiddlePoint: { x: 100, y: 100 },
          size: { width: 120, height: 80 },
          editor: {},
          action: {
            id: "action-call",
            officialType: "Microflows$MicroflowCallAction",
            kind: "callMicroflow",
            caption: "Call target action",
            errorHandlingType: "rollback",
            editor: { category: "call", iconKey: "callMicroflow", availability: "available" },
            parameterMappings: [],
            returnValue: { storeResult: false },
            callMode: "sync",
            ...action
          }
        }
      ]
    }
  } as unknown as MicroflowAuthoringSchema;
}

const resourceIndex: Record<string, StudioMicroflowDefinitionView> = {
  "target-mf": {
    id: "target-mf",
    moduleId: "procurement",
    moduleName: "Procurement",
    name: "MF_Target_V2",
    displayName: "MF Target V2",
    qualifiedName: "Procurement.MF_Target_V2",
    status: "draft",
    schemaId: "schema-target",
    version: "0.1.0",
    referenceCount: 1,
    favorite: false,
    archived: false,
    createdAt: "2026-04-28T00:00:00Z",
    updatedAt: "2026-04-28T00:00:00Z"
  }
};

describe("microflow reference helpers", () => {
  it("parses callMicroflow callees from the current schema", () => {
    const callees = parseMicroflowCallees(
      schemaWithAction({ targetMicroflowId: "target-mf", targetMicroflowQualifiedName: "Procurement.MF_Target" }),
      "source-mf",
      resourceIndex
    );

    expect(callees).toHaveLength(1);
    expect(callees[0]).toMatchObject({
      sourceMicroflowId: "source-mf",
      sourceNodeId: "node-call",
      targetMicroflowId: "target-mf",
      targetMicroflowName: "MF Target V2",
      targetMicroflowQualifiedName: "Procurement.MF_Target_V2",
      stale: false
    });
  });

  it("marks missing targetMicroflowId as stale incomplete call", () => {
    const callees = parseMicroflowCallees(schemaWithAction({ targetMicroflowId: "" }), "source-mf", resourceIndex);

    expect(callees).toHaveLength(1);
    expect(callees[0]).toMatchObject({ stale: true, staleReason: "missingTargetId" });
  });

  it("keeps targetMicroflowId authoritative when stored qualifiedName is stale", () => {
    const callees = parseMicroflowCallees(
      schemaWithAction({ targetMicroflowId: "target-mf", targetMicroflowQualifiedName: "Procurement.OldName" }),
      "source-mf",
      resourceIndex
    );

    expect(callees[0].targetMicroflowId).toBe("target-mf");
    expect(callees[0].targetMicroflowQualifiedName).toBe("Procurement.MF_Target_V2");
    expect(callees[0].storedTargetMicroflowQualifiedName).toBe("Procurement.OldName");
    expect(callees[0].stale).toBe(true);
    expect(callees[0].staleReason).toBe("staleQualifiedName");
  });

  it("does not guess target id from qualifiedName when targetMicroflowId is missing", () => {
    const callees = parseMicroflowCallees(
      schemaWithAction({ targetMicroflowId: "", targetMicroflowQualifiedName: "Procurement.MF_Target_V2" }),
      "source-mf",
      resourceIndex
    );

    expect(callees[0].targetMicroflowId).toBeUndefined();
    expect(callees[0].incomplete).toBe(true);
    expect(callees[0].staleReason).toBe("missingTargetId");
  });

  it("prevents delete when active callers are returned", () => {
    const references: MicroflowReference[] = [
      {
        id: "ref-1",
        targetMicroflowId: "target-mf",
        sourceType: "microflow",
        sourceId: "source-mf",
        sourceName: "MF Source",
        referenceKind: "callMicroflow",
        impactLevel: "medium",
        active: true
      }
    ];

    expect(canDeleteMicroflowFromReferences(references)).toBe(false);
  });

  it("isolates A/B callee parsing by source schema", () => {
    const a = parseMicroflowCallees(schemaWithAction({ targetMicroflowId: "target-mf" }), "source-a", resourceIndex);
    const b = parseMicroflowCallees(schemaWithAction({ targetMicroflowId: "" }), "source-b", resourceIndex);

    expect(a[0].sourceMicroflowId).toBe("source-a");
    expect(a[0].stale).toBe(false);
    expect(b[0].sourceMicroflowId).toBe("source-b");
    expect(b[0].staleReason).toBe("missingTargetId");
  });

  it("resolves source microflow display name from backend reference sourceId", () => {
    const reference: MicroflowReference = {
      id: "ref-2",
      targetMicroflowId: "target-mf",
      sourceType: "microflow",
      sourceId: "target-mf",
      sourceName: "Old Source Name",
      referenceKind: "callMicroflow",
      impactLevel: "medium",
      active: true
    };

    expect(resolveReferenceDisplayName(reference, resourceIndex)).toBe("MF Target V2");
  });
});
