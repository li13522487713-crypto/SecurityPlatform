import { describe, expect, it } from "vitest";
import { resolveWorkspaceEntryTarget } from "./login-entry-helpers";

describe("login-entry-helpers", () => {
  it("prefers last workspace when still accessible", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "200")).toEqual({
      workspaceId: "200",
      target: "/workspace/200/home"
    });
  });

  it("falls back to the first workspace when no recent match exists", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "300")).toEqual({
      workspaceId: "100",
      target: "/workspace/100/home"
    });
  });

  it("returns null when there is no accessible workspace", () => {
    expect(resolveWorkspaceEntryTarget([], null)).toBeNull();
  });
});
