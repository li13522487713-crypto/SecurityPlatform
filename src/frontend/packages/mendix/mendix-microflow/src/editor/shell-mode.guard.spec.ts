import { describe, expect, it } from "vitest";
import { shouldAutoOpenProblemsDock } from "./shell-mode";

describe("shell mode guard", () => {
  it("only auto opens problems dock in legacy host layout", () => {
    expect(shouldAutoOpenProblemsDock("legacy-host-layout")).toBe(true);
    expect(shouldAutoOpenProblemsDock("editor-native-layout")).toBe(false);
  });
});
