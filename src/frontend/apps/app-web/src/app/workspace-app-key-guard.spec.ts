import { describe, expect, it } from "vitest";
import { requiresWorkspaceAppKey } from "./workspace-app-key-guard";

describe("requiresWorkspaceAppKey", () => {
  it("在一级菜单路由下返回 false（无 appKey 也应允许访问）", () => {
    expect(requiresWorkspaceAppKey("/w/ws-1/home")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/develop")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/develop/folder/f-1")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/library")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/manage")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/publish-center")).toBe(false);
    expect(requiresWorkspaceAppKey("/w/ws-1/settings")).toBe(false);
  });

  it("在 Agent / App 详情与发布路由下返回 true（需要 appKey）", () => {
    expect(requiresWorkspaceAppKey("/w/ws-1/agents/a-1")).toBe(true);
    expect(requiresWorkspaceAppKey("/w/ws-1/agents/a-1/publish")).toBe(true);
    expect(requiresWorkspaceAppKey("/w/ws-1/apps/app-1")).toBe(true);
    expect(requiresWorkspaceAppKey("/w/ws-1/apps/app-1/publish")).toBe(true);
    expect(requiresWorkspaceAppKey("/w/ws-1/apps/app-1/workflows/wf-1")).toBe(true);
    expect(requiresWorkspaceAppKey("/w/ws-1/apps/app-1/chatflows/cf-1")).toBe(true);
  });

  it("对非工作区路由返回 false", () => {
    expect(requiresWorkspaceAppKey("/workspace/ws-1/projects")).toBe(false);
    expect(requiresWorkspaceAppKey("/app/app-1/editor")).toBe(false);
    expect(requiresWorkspaceAppKey("/agent/agent-1/editor")).toBe(false);
    expect(requiresWorkspaceAppKey("/market/templates")).toBe(false);
  });
});
