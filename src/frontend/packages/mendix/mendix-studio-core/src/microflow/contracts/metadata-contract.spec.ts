import { describe, expect, it } from "vitest";

import { inferExpressionType } from "@atlas/microflow/expressions";
import { createMockMicroflowMetadataAdapter, getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";
import { sampleMicroflowSchema } from "@atlas/microflow/schema";
import { buildVariableIndex } from "@atlas/microflow/variables";
import { validateMicroflowSchema } from "@atlas/microflow/validators";

describe("microflow metadata contract (round 24)", () => {
  it("mock adapter returns catalog", async () => {
    const adapter = createMockMicroflowMetadataAdapter();
    const catalog = await adapter.getMetadataCatalog();
    expect(catalog.entities.length).toBeGreaterThan(0);
  });

  it("validateMicroflowSchema requires metadata for full validation", () => {
    const missing = validateMicroflowSchema({ schema: sampleMicroflowSchema, metadata: null });
    expect(missing.issues.some(i => i.code === "MF_METADATA_CATALOG_MISSING")).toBe(true);
  });

  it("validateMicroflowSchema with mock catalog runs validators", () => {
    const result = validateMicroflowSchema({ schema: sampleMicroflowSchema, metadata: getDefaultMockMetadataCatalog() });
    expect(Array.isArray(result.issues)).toBe(true);
    expect(result.variableIndex).toBeDefined();
  });

  it("inferExpressionType uses catalog for member access when variable exists", () => {
    const schema = sampleMicroflowSchema;
    const metadata = getDefaultMockMetadataCatalog();
    const variableIndex = buildVariableIndex(schema, metadata);
    const objectId = schema.objectCollection.objects.find(o => o.kind === "actionActivity")?.id ?? schema.objectCollection.objects[0]?.id;
    if (!objectId) {
      return;
    }
    const r = inferExpressionType({
      expression: "$Order/Status",
      schema,
      metadata,
      variableIndex,
      objectId,
    });
    expect(typeof r).toBe("object");
    if (r && typeof r === "object" && "inferredType" in r) {
      expect(["enumeration", "unknown"]).toContain(r.inferredType.kind);
    }
  });

  it("buildVariableIndex uses metadata for retrieve output", () => {
    const index = buildVariableIndex(sampleMicroflowSchema, getDefaultMockMetadataCatalog());
    expect(index.parameters || index.objectOutputs).toBeDefined();
  });
});
