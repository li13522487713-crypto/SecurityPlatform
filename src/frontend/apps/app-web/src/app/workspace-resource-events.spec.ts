import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  consumeWorkspaceResourceCreated,
  notifyWorkspaceResourceCreated
} from "./workspace-resource-events";

describe("workspace-resource-events", () => {
  beforeEach(() => {
    const listeners = new Map<string, Set<(event: Event) => void>>();
    const storage = new Map<string, string>();
    vi.stubGlobal("window", {
      sessionStorage: {
        getItem(key: string) {
          return storage.get(key) ?? null;
        },
        setItem(key: string, value: string) {
          storage.set(key, value);
        },
        clear() {
          storage.clear();
        }
      },
      dispatchEvent(event: Event) {
        listeners.get(event.type)?.forEach(listener => listener(event));
        return true;
      },
      addEventListener(type: string, listener: (event: Event) => void) {
        const bucket = listeners.get(type) ?? new Set<(event: Event) => void>();
        bucket.add(listener);
        listeners.set(type, bucket);
      },
      removeEventListener(type: string, listener: (event: Event) => void) {
        listeners.get(type)?.delete(listener);
      }
    });
  });

  afterEach(() => {
    window.sessionStorage.clear();
    vi.unstubAllGlobals();
  });

  it("按工作区消费待处理的已创建资源", () => {
    notifyWorkspaceResourceCreated({
      workspaceId: "ws-1",
      resourceType: "app",
      resourceId: "app-1",
      resourceName: "App One"
    });
    notifyWorkspaceResourceCreated({
      workspaceId: "ws-2",
      resourceType: "agent",
      resourceId: "agent-1",
      resourceName: "Agent One"
    });

    const ws1Items = consumeWorkspaceResourceCreated("ws-1");
    const ws2Items = consumeWorkspaceResourceCreated("ws-2");

    expect(ws1Items).toHaveLength(1);
    expect(ws1Items[0]?.resourceId).toBe("app-1");
    expect(ws2Items).toHaveLength(1);
    expect(ws2Items[0]?.resourceId).toBe("agent-1");
  });

  it("消费后不会重复返回同一条记录", () => {
    notifyWorkspaceResourceCreated({
      workspaceId: "ws-1",
      resourceType: "app",
      resourceId: "app-2",
      resourceName: "App Two"
    });

    expect(consumeWorkspaceResourceCreated("ws-1")).toHaveLength(1);
    expect(consumeWorkspaceResourceCreated("ws-1")).toHaveLength(0);
  });
});
