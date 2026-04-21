import { describe, expect, it } from "vitest";
import {
  buildWorkspaceWorkbenchPath,
  resolveLegacyAppRedirectTarget
} from "./legacy-route-mapping";

describe("legacy route mapping", () => {
  it("maps legacy studio dashboard and admin routes to canonical workspace routes", () => {
    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/studio/dashboard",
      searchText: ""
    })).toBe("/w/workspace-1/home");

    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/admin/users",
      searchText: ""
    })).toBe("/w/workspace-1/manage/users");

    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/admin/profile",
      searchText: ""
    })).toBe("/w/workspace-1/settings/profile");
  });

  it("maps legacy studio resource routes to canonical workspace resource routes", () => {
    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/studio/apps/app-9/publish",
      searchText: ""
    })).toBe("/w/workspace-1/apps/app-9/publish");

    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/studio/assistants/bot-7",
      searchText: ""
    })).toBe("/w/workspace-1/agents/bot-7");

    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/space/atlas-space/model-configs",
      searchText: ""
    })).toBe("/w/workspace-1/develop/model-configs");
  });

  it("builds canonical workbench targets from legacy workflow paths", () => {
    expect(resolveLegacyAppRedirectTarget({
      orgId: "tenant-1",
      workspaceId: "workspace-1",
      relativePath: "/work_flow/flow-1/editor",
      searchText: "?contentMode=session"
    })).toBe("/w/workspace-1/workflows/flow-1?contentMode=session");

    expect(buildWorkspaceWorkbenchPath("tenant-1", "workspace-1", "chatflow", "flow-2", "variables")).toBe(
      "/w/workspace-1/chatflows/flow-2?contentMode=variables"
    );
  });
});
