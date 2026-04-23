// @vitest-environment jsdom

import type { ReactNode } from "react";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppSetupPage, PlatformNotReadyPage } from "./status-page";

const bootstrapState = {
  loading: false,
  platformReady: true,
  appReady: false,
  refresh: vi.fn(async () => undefined)
};

const authState = {
  loading: false,
  isAuthenticated: false
};

const { getDriversMock } = vi.hoisted(() => ({
  getDriversMock: vi.fn(async () => ({ success: true, data: [] }))
}));

vi.mock("@douyinfe/semi-ui", () => ({
  Button: ({ children, onClick }: { children?: ReactNode; onClick?: () => void }) => (
    <button type="button" onClick={onClick}>{children}</button>
  ),
  Checkbox: ({ children }: { children?: ReactNode }) => <label>{children}</label>,
  Descriptions: ({ data }: { data?: Array<{ key: ReactNode; value: ReactNode }> }) => (
    <div>{data?.map(item => <div key={String(item.key)}>{item.value}</div>)}</div>
  ),
  Input: ({ value }: { value?: string }) => <input value={value ?? ""} readOnly />,
  InputNumber: ({ value }: { value?: number }) => <input value={String(value ?? "")} readOnly />,
  Radio: ({ children }: { children?: ReactNode }) => <label>{children}</label>,
  RadioGroup: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Select: ({ value }: { value?: string }) => (
    <select value={value ?? ""} onChange={() => undefined}>
      <option value={value ?? ""}>{value ?? ""}</option>
    </select>
  ),
  Space: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Tag: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
  TextArea: ({ value }: { value?: string }) => <textarea value={value ?? ""} readOnly />,
  Typography: {
    Title: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
    Text: ({ children }: { children?: ReactNode }) => <span>{children}</span>
  }
}));

vi.mock("../bootstrap-context", () => ({
  useBootstrap: () => bootstrapState
}));

vi.mock("../auth-context", () => ({
  useAuth: () => authState
}));

vi.mock("../i18n", () => ({
  useAppI18n: () => ({
    locale: "zh-CN" as const,
    setLocale: vi.fn(),
    t: (key: string) => key
  })
}));

vi.mock("../../services/api-core", () => ({
  rememberConfiguredAppKey: vi.fn()
}));

vi.mock("../../services/api-setup", () => ({
  getDrivers: getDriversMock,
  initializeApp: vi.fn(),
  testConnection: vi.fn()
}));

vi.mock("../_shared", () => ({
  FormCard: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  InfoBanner: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  PageShell: ({ children, loading, loadingTip, testId }: { children?: ReactNode; loading?: boolean; loadingTip?: string; testId?: string }) => (
    <div data-testid={testId ?? "page-shell"}>{loading ? loadingTip ?? "loading" : children}</div>
  ),
  ResultCard: ({
    title,
    description,
    actions
  }: {
    title?: ReactNode;
    description?: ReactNode;
    actions?: ReactNode;
  }) => (
    <div>
      <div>{title}</div>
      <div>{description}</div>
      <div>{actions}</div>
    </div>
  ),
  SectionCard: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  StateBadge: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  StepsBar: () => <div>steps</div>
}));

function renderStatusPage(path: "/app-setup" | "/platform-not-ready") {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/app-setup" element={<AppSetupPage />} />
        <Route path="/platform-not-ready" element={<PlatformNotReadyPage />} />
        <Route path="/sign" element={<div data-testid="sign-page">sign</div>} />
        <Route path="/console" element={<div data-testid="workspace-page">workspace</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe("startup status pages", () => {
  afterEach(() => {
    cleanup();
  });

  beforeEach(() => {
    bootstrapState.loading = false;
    bootstrapState.platformReady = true;
    bootstrapState.appReady = false;
    bootstrapState.refresh.mockClear();
    authState.loading = false;
    authState.isAuthenticated = false;
    getDriversMock.mockClear();
  });

  it("AppSetupPage 在 app 已就绪且未登录时直接跳 sign", async () => {
    bootstrapState.appReady = true;
    renderStatusPage("/app-setup");
    await waitFor(() => {
      expect(screen.getByTestId("sign-page").textContent).toBe("sign");
    });
  });

  it("AppSetupPage 在 app 已就绪且已登录时直接跳 select-workspace", async () => {
    bootstrapState.appReady = true;
    authState.isAuthenticated = true;
    renderStatusPage("/app-setup");
    await waitFor(() => {
      expect(screen.getByTestId("workspace-page").textContent).toBe("workspace");
    });
  });

  it("PlatformNotReadyPage 在平台未就绪时保持当前页", () => {
    bootstrapState.platformReady = false;
    renderStatusPage("/platform-not-ready");
    expect(screen.getByText("platformNotReadyTitle").textContent).toBe("platformNotReadyTitle");
    expect(screen.getByText("platformNotReadyDesc").textContent).toBe("platformNotReadyDesc");
  });

  it("PlatformNotReadyPage 在平台恢复但应用未就绪时跳到 app-setup", async () => {
    renderStatusPage("/platform-not-ready");
    await waitFor(() => {
      expect(screen.getByTestId("app-setup-page").textContent).toContain("steps");
    });
  });
});
