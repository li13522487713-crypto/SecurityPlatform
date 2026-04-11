import { describe, expect, it } from "vitest";
import { buildVariableSuggestions, deriveOutputKeys } from "./smoke-utils";

describe("smoke-utils", () => {
  it("deriveOutputKeys should return configured output key first", () => {
    const keys = deriveOutputKeys("IntentDetector", {
      ai: {
        outputKey: "intent"
      }
    });
    expect(keys).toContain("intent");
  });

  it("deriveOutputKeys should return default fallback key when missing", () => {
    const keys = deriveOutputKeys("Llm", {});
    expect(keys).toEqual(["result"]);
  });

  it("buildVariableSuggestions should skip selected node and include template tokens", () => {
    const suggestions = buildVariableSuggestions(
      [
        { key: "entry_1", type: "Entry", configs: { io: { key: "input" } }, x: 100 },
        { key: "llm_1", type: "Llm", configs: { llm: { outputKey: "answer" } }, x: 500 },
        { key: "exit_1", type: "Exit", configs: {}, x: 900 }
      ],
      "exit_1"
    );
    expect(suggestions.find((item) => item.value === "{{entry_1.input}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value === "{{llm_1.answer}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value.startsWith("{{exit_1."))).toBeFalsy();
  });

  it("buildVariableSuggestions should ignore downstream node by x order", () => {
    const suggestions = buildVariableSuggestions(
      [
        { key: "entry_1", type: "Entry", configs: { io: { key: "input" } }, x: 100 },
        { key: "llm_1", type: "Llm", configs: { llm: { outputKey: "answer" } }, x: 700 },
        { key: "selector_1", type: "Selector", configs: {}, x: 300 }
      ],
      "selector_1"
    );
    expect(suggestions.find((item) => item.value === "{{entry_1.input}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value === "{{llm_1.answer}}")).toBeFalsy();
  });

  it("buildVariableSuggestions should only include DAG upstream nodes when connections provided", () => {
    const suggestions = buildVariableSuggestions(
      [
        { key: "entry_1", type: "Entry", configs: { io: { key: "input" } }, x: 100 },
        { key: "llm_1", type: "Llm", configs: { llm: { outputKey: "answer" } }, x: 300 },
        { key: "isolated_1", type: "CodeRunner", configs: { processor: { outputKey: "ignored" } }, x: 120 }
      ],
      "llm_1",
      [{ fromNode: "entry_1", toNode: "llm_1" }]
    );

    expect(suggestions.find((item) => item.value === "{{entry_1.input}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value === "{{isolated_1.ignored}}")).toBeFalsy();
  });

  it("buildVariableSuggestions should include global variables", () => {
    const suggestions = buildVariableSuggestions(
      [{ key: "entry_1", type: "Entry", configs: {}, x: 100 }],
      "entry_1",
      [],
      { tenantName: "atlas", threshold: 80 }
    );

    expect(suggestions.find((item) => item.value === "{{global.tenantName}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value === "{{global.threshold}}")).toBeTruthy();
  });
});

