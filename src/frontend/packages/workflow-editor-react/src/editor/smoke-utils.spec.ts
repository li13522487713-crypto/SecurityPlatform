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
});

