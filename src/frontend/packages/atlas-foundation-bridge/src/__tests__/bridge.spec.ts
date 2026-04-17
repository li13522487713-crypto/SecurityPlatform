// @vitest-environment jsdom

import { afterEach, describe, expect, it } from "vitest";
import {
  getAtlasFoundationHost,
  resetAtlasFoundationHost,
  setAtlasFoundationHost,
  subscribeAtlasFoundationHost,
} from "../atlas-host-bridge";
import {
  getLoginStatus,
  getUserInfo,
  refreshUserInfo,
  uploadAvatar,
  useCurrentTheme,
  useLoginStatus,
  useSpace,
  useUserInfo,
} from "../index";
import { renderHook } from "../testing/render-hook";

describe("atlas-foundation-bridge", () => {
  afterEach(() => {
    resetAtlasFoundationHost();
  });

  it("默认状态在未注入快照时返回 settling/light/null/[]", () => {
    expect(getLoginStatus()).toBe("settling");
    expect(getUserInfo()).toBeNull();
    expect(getAtlasFoundationHost().spaces).toEqual([]);
  });

  it("setAtlasFoundationHost 仅在状态变化时通知监听器", () => {
    let calls = 0;
    const dispose = subscribeAtlasFoundationHost(() => {
      calls += 1;
    });

    setAtlasFoundationHost({
      loginStatus: "logined",
      user: {
        userIdStr: "1001",
        name: "alice",
        screenName: "alice",
        tenantId: "00000000-0000-0000-0000-000000000001",
      },
    });
    setAtlasFoundationHost({ loginStatus: "logined" });

    dispose();
    expect(calls).toBe(1);
  });

  it("useUserInfo / useLoginStatus 会在快照更新后重新返回", () => {
    setAtlasFoundationHost({
      loginStatus: "logined",
      user: {
        userIdStr: "1001",
        name: "alice",
        screenName: "alice",
      },
    });

    const { result } = renderHook(() => ({
      status: useLoginStatus(),
      user: useUserInfo(),
      theme: useCurrentTheme(),
    }));

    expect(result.current.status).toBe("logined");
    expect(result.current.user?.user_id_str).toBe("1001");
    expect(result.current.user?.name).toBe("alice");
    expect(result.current.theme).toBe("light");
  });

  it("useSpace 优先返回入参 id，缺失时按 activeSpaceId 与首项兜底", () => {
    setAtlasFoundationHost({
      spaces: [
        { id: "ws-1", name: "Default", spaceType: 2 },
        { id: "ws-2", name: "Backup", spaceType: 2, roleType: 2 },
      ],
      activeSpaceId: "ws-2",
    });

    const exact = renderHook(() => useSpace("ws-2"));
    expect(exact.result.current?.id).toBe("ws-2");
    expect(exact.result.current?.role_type).toBe(2);

    const fallback = renderHook(() => useSpace("non-existing"));
    expect(fallback.result.current?.id).toBe("ws-2");
  });

  it("logout / refresh / uploadAvatar 等占位实现不抛错", async () => {
    await expect(refreshUserInfo()).resolves.toBeUndefined();
    const result = await uploadAvatar(new File([new Uint8Array([0])], "avatar.png"));
    expect(result.web_uri).toBe("");
  });

  it("useSpace 在宿主只注入 id 时给出 cozelib 可消费的兜底字段", () => {
    setAtlasFoundationHost({
      spaces: [
        {
          id: "ws-only-id",
          name: "",
          spaceType: 0 as unknown as 2,
        },
      ],
      activeSpaceId: "ws-only-id",
    });

    const { result } = renderHook(() => useSpace("ws-only-id"));
    // 字段缺失时一律走 Team / Normal / Member 兜底，避免 cozelib 在 space_type === 1 / 2 分支拿到 0/undefined。
    expect(result.current?.id).toBe("ws-only-id");
    expect(result.current?.name).toBe("ws-only-id");
    expect(result.current?.description).toBe("");
    expect(result.current?.icon_url).toBe("");
    expect(result.current?.space_type).toBe(2);
    expect(result.current?.space_mode).toBe(0);
    expect(result.current?.role_type).toBe(3);
  });
});
