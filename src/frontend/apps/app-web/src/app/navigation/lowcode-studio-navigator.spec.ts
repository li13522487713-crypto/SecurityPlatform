// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import {
  buildLowcodeStudioUrl,
  navigateToLowcodeStudio,
  resolveLowcodeStudioOrigin
} from "./lowcode-studio-navigator";

describe("lowcode-studio-navigator", () => {
  it("未配置 origin 时返回同源路径", () => {
    expect(buildLowcodeStudioUrl("100", {})).toBe("/apps/lowcode/100/studio");
  });

  it("配置 origin 时返回绝对地址", () => {
    const env = { VITE_LOWCODE_STUDIO_ORIGIN: "https://studio.atlas.local" };
    expect(resolveLowcodeStudioOrigin(env)).toBe("https://studio.atlas.local");
    expect(buildLowcodeStudioUrl("100", env)).toBe("https://studio.atlas.local/apps/lowcode/100/studio");
  });

  it("origin 非法时回退同源路径", () => {
    const env = { VITE_LOWCODE_STUDIO_ORIGIN: "not-a-valid-url" };
    expect(resolveLowcodeStudioOrigin(env)).toBeNull();
    expect(buildLowcodeStudioUrl("100", env)).toBe("/apps/lowcode/100/studio");
  });

  it("navigateToLowcodeStudio 会触发 location.assign", () => {
    const assignSpy = vi.fn();
    const url = navigateToLowcodeStudio(
      "100",
      { VITE_LOWCODE_STUDIO_ORIGIN: "https://studio.atlas.local" },
      { assign: assignSpy }
    );
    expect(url).toBe("https://studio.atlas.local/apps/lowcode/100/studio");
    expect(assignSpy).toHaveBeenCalledWith("https://studio.atlas.local/apps/lowcode/100/studio");
  });
});
