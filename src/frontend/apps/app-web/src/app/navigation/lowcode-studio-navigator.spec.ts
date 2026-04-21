// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import {
  buildLowcodeStudioUrl,
  navigateToLowcodeStudio,
  resolveLowcodeStudioOrigin
} from "./lowcode-studio-navigator";

describe("lowcode-studio-navigator", () => {
  it("未配置 origin 时按当前主机推导 lowcode studio 端口", () => {
    const locationLike = { href: "http://172.18.0.1:5181/workspace/1496199449293099008/projects" };
    expect(resolveLowcodeStudioOrigin({}, locationLike)).toBe("http://172.18.0.1:5183");
    expect(buildLowcodeStudioUrl("100", {}, locationLike)).toBe("http://172.18.0.1:5183/apps/lowcode/100/studio");
  });

  it("配置 origin 时返回绝对地址", () => {
    const env = { VITE_LOWCODE_STUDIO_ORIGIN: "https://studio.atlas.local" };
    const locationLike = { href: "http://172.18.0.1:5181/workspace/100/projects" };
    expect(resolveLowcodeStudioOrigin(env, locationLike)).toBe("https://studio.atlas.local");
    expect(buildLowcodeStudioUrl("100", env, locationLike)).toBe("https://studio.atlas.local/apps/lowcode/100/studio");
  });

  it("origin 非法时回退到端口推导逻辑", () => {
    const env = { VITE_LOWCODE_STUDIO_ORIGIN: "not-a-valid-url", VITE_LOWCODE_STUDIO_PORT: "6199" };
    const locationLike = { href: "http://127.0.0.1:5181/workspace/100/projects" };
    expect(resolveLowcodeStudioOrigin(env, locationLike)).toBe("http://127.0.0.1:6199");
    expect(buildLowcodeStudioUrl("100", env, locationLike)).toBe("http://127.0.0.1:6199/apps/lowcode/100/studio");
  });

  it("navigateToLowcodeStudio 会触发 location.assign", () => {
    const assignSpy = vi.fn();
    const result = navigateToLowcodeStudio(
      "100",
      { VITE_LOWCODE_STUDIO_ORIGIN: "https://studio.atlas.local" },
      { assign: assignSpy, href: "http://127.0.0.1:5181/workspace/100/projects" }
    );
    expect(result).toEqual({
      target: "https://studio.atlas.local/apps/lowcode/100/studio",
      redirected: true
    });
    expect(assignSpy).toHaveBeenCalledWith("https://studio.atlas.local/apps/lowcode/100/studio");
  });

  it("当目标地址与当前地址一致时停止重复跳转", () => {
    const assignSpy = vi.fn();
    const result = navigateToLowcodeStudio(
      "100",
      { VITE_LOWCODE_STUDIO_ORIGIN: "https://studio.atlas.local" },
      { assign: assignSpy, href: "https://studio.atlas.local/apps/lowcode/100/studio" }
    );
    expect(result).toEqual({
      target: "https://studio.atlas.local/apps/lowcode/100/studio",
      redirected: false,
      reason: "already-at-target"
    });
    expect(assignSpy).not.toHaveBeenCalled();
  });
});
