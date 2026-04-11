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
        { key: "entry_1", type: "Entry", configs: { io: { key: "input" } } },
        { key: "llm_1", type: "Llm", configs: { llm: { outputKey: "answer" } } },
        { key: "exit_1", type: "Exit", configs: {} }
      ],
      "exit_1"
    );
    expect(suggestions.find((item) => item.value === "{{entry_1.input}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value === "{{llm_1.answer}}")).toBeTruthy();
    expect(suggestions.find((item) => item.value.startsWith("{{exit_1."))).toBeFalsy();
  });
});

