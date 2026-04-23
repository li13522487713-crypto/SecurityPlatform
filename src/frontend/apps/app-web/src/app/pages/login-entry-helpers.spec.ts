import { describe, expect, it } from "vitest";
import { resolveWorkspaceEntryTarget } from "./login-entry-helpers";

describe("login-entry-helpers", () => {
  it("prefers last workspace when still accessible", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "200")).toEqual({
      workspaceId: "200",
      target: "/space/200/projects"
    });
  });

  it("falls back to the first workspace when no recent match exists", () => {
    expect(resolveWorkspaceEntryTarget(["100", "200"], "300")).toEqual({
      workspaceId: "100",
      target: "/space/100/projects"
    });
  });

  it("returns null when there is no accessible workspace", () => {
    expect(resolveWorkspaceEntryTarget([], null)).toBeNull();
  });
});
