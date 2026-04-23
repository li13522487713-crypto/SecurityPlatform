import { describe, expect, it } from "vitest";
import { resolveWorkspaceEntryTarget } from "./login-entry-helpers";

describe("login-entry-helpers", () => {
  it("prefers last workspace when still accessible", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "200")).toBe("/workspace/200/home");
  });

  it("falls back to the only workspace when there is exactly one", () => {
    expect(resolveWorkspaceEntryTarget(["100"], null)).toBe("/workspace/100/home");
  });

  it("falls back to workspace selection when multiple remain and no recent match", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "300")).toBe("/select-workspace");
  });
});
