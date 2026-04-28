// @vitest-environment jsdom

import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HomePage } from "./home-page";

const bootstrapState = {
  loading: false,
  platformReady: true,
  appReady: true
};

const authState = {
  loading: false,
  isAuthenticated: false
};

vi.mock("../bootstrap-context", () => ({
  useBootstrap: () => bootstrapState
}));

vi.mock("../auth-context", () => ({
  useAuth: () => authState
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    t: (key: string) => key
  })
}));

vi.mock("../_shared", () => ({
  PageShell: ({ loadingTip }: { loadingTip?: string }) => <div data-testid="home-loading">{loadingTip ?? "loading"}</div>
}));

function renderHomePage() {
  render(
    <MemoryRouter initialEntries={["/"]}>
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/platform-not-ready" element={<div data-testid="platform-page">platform</div>} />
        <Route path="/app-setup" element={<div data-testid="app-setup-page">app-setup</div>} />
        <Route path="/sign" element={<div data-testid="sign-page">sign</div>} />
        <Route path="/console" element={<div data-testid="console-page">console</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe("HomePage", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    bootstrapState.loading = false;
    bootstrapState.platformReady = true;
    bootstrapState.appReady = true;
    authState.loading = false;
    authState.isAuthenticated = false;
  });

  it("loading 时显示加载页", () => {
    bootstrapState.loading = true;
    renderHomePage();
    expect(screen.getByTestId("home-loading").textContent).toBe("loading");
  });

  it("平台未就绪时只跳到 platform-not-ready", async () => {
    bootstrapState.platformReady = false;
    renderHomePage();
    await waitFor(() => {
      expect(screen.getByTestId("platform-page").textContent).toBe("platform");
    });
  });

  it("应用未就绪时只跳到 app-setup", async () => {
    bootstrapState.appReady = false;
    renderHomePage();
    await waitFor(() => {
      expect(screen.getByTestId("app-setup-page").textContent).toBe("app-setup");
    });
  });

  it("未登录时跳到 sign", async () => {
    renderHomePage();
    await waitFor(() => {
      expect(screen.getByTestId("sign-page").textContent).toBe("sign");
    });
  });

  it("已登录时跳到控制台入口", async () => {
    authState.isAuthenticated = true;
    renderHomePage();
    await waitFor(() => {
      expect(screen.getByTestId("console-page").textContent).toBe("console");
    });
  });
});
