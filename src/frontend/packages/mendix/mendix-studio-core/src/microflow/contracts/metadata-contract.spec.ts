import { describe, expect, it } from "vitest";

import { createMockMicroflowMetadataAdapter, getDefaultMockMetadataCatalog } from "@atlas/microflow/metadata";

describe("microflow metadata contract", () => {
  it("mock adapter returns catalog", async () => {
    const adapter = createMockMicroflowMetadataAdapter();
    const catalog = await adapter.getMetadataCatalog();
    expect(catalog.entities.length).toBeGreaterThan(0);
  });

  it("default catalog exposes entities and callable microflows for design-time selectors", () => {
    const catalog = getDefaultMockMetadataCatalog();
    expect(catalog.entities.length).toBeGreaterThan(0);
    expect(catalog.microflows.length).toBeGreaterThan(0);
  });
});
