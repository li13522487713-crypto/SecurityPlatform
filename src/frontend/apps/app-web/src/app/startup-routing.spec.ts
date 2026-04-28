import { describe, expect, it } from "vitest";
import { resolveStartupRedirectTarget, STARTUP_ROUTE_PATHS } from "./startup-routing";

describe("resolveStartupRedirectTarget", () => {
  it("loading 中不跳转", () => {
    expect(resolveStartupRedirectTarget({
      pathname: "/",
      bootstrap: { loading: true, platformReady: false, appReady: false },
      auth: { loading: false, isAuthenticated: false }
    })).toBeNull();
  });

  it("平台未就绪时命中 platform-not-ready", () => {
    expect(resolveStartupRedirectTarget({
      pathname: "/",
      bootstrap: { loading: false, platformReady: false, appReady: false },
      auth: { loading: false, isAuthenticated: false }
    })).toBe(STARTUP_ROUTE_PATHS.platformNotReady);
  });

  it("应用未就绪时命中 app-setup", () => {
    expect(resolveStartupRedirectTarget({
      pathname: "/",
      bootstrap: { loading: false, platformReady: true, appReady: false },
      auth: { loading: false, isAuthenticated: false }
    })).toBe(STARTUP_ROUTE_PATHS.appSetup);
  });

  it("应用已就绪但未登录时命中 sign", () => {
    expect(resolveStartupRedirectTarget({
      pathname: "/",
      bootstrap: { loading: false, platformReady: true, appReady: true },
      auth: { loading: false, isAuthenticated: false }
    })).toBe(STARTUP_ROUTE_PATHS.sign);
  });

  it("应用已就绪且已登录时命中 select-workspace", () => {
    expect(resolveStartupRedirectTarget({
      pathname: "/",
      bootstrap: { loading: false, platformReady: true, appReady: true },
      auth: { loading: false, isAuthenticated: true }
    })).toBe(STARTUP_ROUTE_PATHS.selectWorkspace);
  });

  it("当前已在正确状态页时返回 null", () => {
    expect(resolveStartupRedirectTarget({
      pathname: STARTUP_ROUTE_PATHS.platformNotReady,
      bootstrap: { loading: false, platformReady: false, appReady: false },
      auth: { loading: false, isAuthenticated: false }
    })).toBeNull();

    expect(resolveStartupRedirectTarget({
      pathname: STARTUP_ROUTE_PATHS.appSetup,
      bootstrap: { loading: false, platformReady: true, appReady: false },
      auth: { loading: false, isAuthenticated: false }
    })).toBeNull();

    expect(resolveStartupRedirectTarget({
      pathname: STARTUP_ROUTE_PATHS.sign,
      bootstrap: { loading: false, platformReady: true, appReady: true },
      auth: { loading: false, isAuthenticated: false }
    })).toBeNull();

    expect(resolveStartupRedirectTarget({
      pathname: STARTUP_ROUTE_PATHS.selectWorkspace,
      bootstrap: { loading: false, platformReady: true, appReady: true },
      auth: { loading: false, isAuthenticated: true }
    })).toBeNull();
  });
});
