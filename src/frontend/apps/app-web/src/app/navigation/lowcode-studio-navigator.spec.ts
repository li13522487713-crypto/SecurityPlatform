// @vitest-environment jsdom

import { describe, expect, it, vi } from "vitest";
import {
  buildLowcodeStudioPath,
  buildLowcodeStudioUrl,
  navigateToLowcodeStudio,
  resolveLowcodeStudioOrigin
} from "./lowcode-studio-navigator";

describe("lowcode-studio-navigator", () => {
  it("始终生成 app-web 壳内 studio 路径", () => {
    expect(resolveLowcodeStudioOrigin()).toBeNull();
    expect(buildLowcodeStudioPath("100")).toBe("/apps/lowcode/100/studio");
    expect(buildLowcodeStudioUrl("100")).toBe("/apps/lowcode/100/studio");
  });

  it("提供 navigate 时优先走站内路由导航", () => {
    const navigate = vi.fn();
    const result = navigateToLowcodeStudio("100", navigate);
    expect(result).toEqual({
      target: "/apps/lowcode/100/studio",
      redirected: true
    });
    expect(navigate).toHaveBeenCalledWith("/apps/lowcode/100/studio");
  });

  it("当目标地址与当前地址一致时停止重复跳转", () => {
    const assignSpy = vi.fn();
    const result = navigateToLowcodeStudio(
      "100",
      undefined,
      { assign: assignSpy, href: "http://127.0.0.1:5181/apps/lowcode/100/studio" }
    );
    expect(result).toEqual({
      target: "/apps/lowcode/100/studio",
      redirected: false,
      reason: "already-at-target"
    });
    expect(assignSpy).not.toHaveBeenCalled();
  });

  it("没有 navigate 时回退到同源 location.assign", () => {
    const assignSpy = vi.fn();
    const result = navigateToLowcodeStudio(
      "100",
      undefined,
      { assign: assignSpy, href: "http://127.0.0.1:5181/workspace/100/projects" }
    );
    expect(result).toEqual({
      target: "/apps/lowcode/100/studio",
      redirected: true
    });
    expect(assignSpy).toHaveBeenCalledWith("/apps/lowcode/100/studio");
  });
});
