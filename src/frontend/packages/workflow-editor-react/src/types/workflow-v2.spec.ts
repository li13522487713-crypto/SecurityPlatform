import { describe, expect, it } from "vitest";
import {
  normalizeNodeTypeKey,
  workflowNodeTypeToValue,
  workflowNodeValueToKey,
  WORKFLOW_SCHEMA_VERSION
} from "./workflow-v2";

describe("workflow-v2 node type mapping", () => {
  it("maps alias names to canonical node keys", () => {
    expect(normalizeNodeTypeKey("Start")).toBe("Entry");
    expect(normalizeNodeTypeKey("End")).toBe("Exit");
    expect(normalizeNodeTypeKey("LLM")).toBe("Llm");
    expect(normalizeNodeTypeKey("Api")).toBe("Plugin");
    expect(normalizeNodeTypeKey("If")).toBe("Selector");
  });

  it("maps numeric coze-style values to canonical node keys", () => {
    expect(normalizeNodeTypeKey("1")).toBe("Entry");
    expect(normalizeNodeTypeKey("2")).toBe("Exit");
    expect(normalizeNodeTypeKey("45")).toBe("HttpRequester");
    expect(normalizeNodeTypeKey("59")).toBe("JsonDeserialization");
  });

  it("supports value round-trip for known keys", () => {
    const key = "IntentDetector";
    const value = workflowNodeTypeToValue(key);
    expect(workflowNodeValueToKey(value)).toBe(key);
  });

  it("keeps current schema version", () => {
    expect(WORKFLOW_SCHEMA_VERSION).toBeGreaterThanOrEqual(2);
  });
});
